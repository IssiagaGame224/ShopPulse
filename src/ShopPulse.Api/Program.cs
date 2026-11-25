using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using ShopPulse.Api;
using ShopPulse.Api.Hubs;
using ShopPulse.Contracts;
using ShopPulse.Domain;
using ShopPulse.Infrastructure;
using ShopPulse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Timeouts;   // .NET 10 : RequestTimeoutPolicy a déménagé (était dans Microsoft.AspNetCore.Http en .NET 8)
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Observabilité : logs structurés Serilog (niveaux depuis la configuration,
// enrichis du nom d'application ; TraceId ajouté par requête via UseSerilogRequestLogging).
builder.Host.UseSerilog((ctx, cfg) => cfg
       .ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .Enrich.WithProperty("Application", "ShopPulse.Api")
       .WriteTo.Console(outputTemplate:
              "[{Timestamp:HH:mm:ss} {Level:u3}] [{Application}] {Message:lj}{NewLine}{Exception}"));

// Health checks : la base ET l'IdP doivent répondre pour que l'API soit « Healthy ».
builder.Services.AddHealthChecks()
       .AddNpgSql(builder.Configuration.GetConnectionString("ShopPulse")!, name: "postgresql")
       .AddUrlGroup(
              new Uri($"{builder.Configuration["Oidc:Authority"]}/.well-known/openid-configuration"),
              name: "keycloak");

// Métriques OpenTelemetry : requêtes HTTP entrantes/sortantes, exposées au format
// Prometheus sur /metrics (à protéger par le réseau en production).
// Traces distribuées (docs/25) : Web → API → PostgreSQL dans une même trace, exportées
// en OTLP vers un collecteur (Jaeger) UNIQUEMENT si un endpoint est configuré
// (OTEL_EXPORTER_OTLP_ENDPOINT) — sinon aucun export, aucune erreur de connexion.
builder.Services.AddOpenTelemetry()
       .ConfigureResource(r => r.AddService("ShopPulse.Api"))
       .WithMetrics(m => m
              .AddAspNetCoreInstrumentation()
              .AddHttpClientInstrumentation()
              .AddPrometheusExporter())
       .WithTracing(t =>
       {
              t.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddSource("Npgsql");          // spans des requêtes SQL (ActivitySource intégré à Npgsql)
              if (builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is not null)
                     t.AddOtlpExporter();     // lit OTEL_EXPORTER_OTLP_ENDPOINT (gRPC, 4317)
       });

// Persistance : PostgreSQL via EF Core (phase « vraie boutique », remplace l'in-memory).
// Chaîne de connexion : appsettings.Development.json en local, ConnectionStrings__ShopPulse
// (variable d'environnement) en conteneur/production — jamais de secret dans le dépôt.
builder.Services.AddDbContext<ShopPulseDbContext>(o =>
       o.UseNpgsql(builder.Configuration.GetConnectionString("ShopPulse")));

// Cache serveur (décorateur, module 2) devant la lecture EF ; l'invalidation se fait
// par tag "catalog" à chaque écriture (règle d'or : toute promesse d'invalidation).
builder.Services.AddScoped<EfProductService>();
builder.Services.AddScoped<ShopPulse.Application.IProductService>(sp =>
       new CachedProductService(sp.GetRequiredService<EfProductService>(), sp.GetRequiredService<HybridCache>()));
builder.Services.AddHybridCache();

// Commandes (phase 3) : création transactionnelle + email de confirmation (Mailpit en dev).
builder.Services.AddScoped<EfOrderService>();
builder.Services.AddScoped<ShopPulse.Application.IOrderPlacer>(sp => sp.GetRequiredService<EfOrderService>());
builder.Services.AddScoped<ShopPulse.Application.IOrderAdmin>(sp => sp.GetRequiredService<EfOrderService>());
builder.Services.AddSingleton<ShopPulse.Application.IOrderEmailSender, SmtpOrderEmailSender>();
// Paramètres commerciaux (livraison, TVA) : source unique lue par l'API et exposée au Web.
builder.Services.AddSingleton<ShopPulse.Application.IStoreConfigProvider, StoreConfigProvider>();
// Outbox : les emails transactionnels sont écrits en base dans la transaction métier,
// puis envoyés de façon asynchrone par ce worker (jamais perdus si le SMTP est en panne).
builder.Services.AddHostedService<OutboxWorker>();

// Paiement (phase 6) : Stripe si les clés sont configurées (user-secrets / env),
// sinon simulateur de dev — la boucle complète reste testable sans compte Stripe.
if (!string.IsNullOrEmpty(builder.Configuration["Stripe:SecretKey"]))
{
       builder.Services.AddSingleton<ShopPulse.Application.IPaymentGateway>(sp =>
              new StripePaymentGateway(
                     builder.Configuration["Stripe:SecretKey"]!,
                     builder.Configuration["Stripe:PublishableKey"]!,
                     builder.Configuration["Stripe:WebhookSecret"]!));
}
else
{
       builder.Services.AddSingleton<ShopPulse.Application.IPaymentGateway, FakePaymentGateway>();
}
// Énumérations en clair dans le JSON ("Pending" plutôt que 0) : API lisible par des humains.
builder.Services.ConfigureHttpJsonOptions(o =>
       o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Temps réel.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IStockPublisher, StockPublisher>();

// Erreurs : Problem Details (RFC 9457).
builder.Services.AddProblemDetails();

// Comptes clients (phase 4) : l'API valide les jetons d'accès Keycloak (JWT bearer).
// Les endpoints publics restent anonymes ; les endpoints « compte » exigent un jeton.
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(o =>
       {
              o.Authority = builder.Configuration["Oidc:Authority"];
              o.RequireHttpsMetadata = builder.Configuration.GetValue("Oidc:RequireHttpsMetadata",
                     !builder.Environment.IsDevelopment());
              // DNS split-horizon (conteneurs) : métadonnées internes, émetteur public.
              if (builder.Configuration["Oidc:ValidIssuer"] is { } validIssuer)
                     o.TokenValidationParameters.ValidIssuer = validIssuer;
              // Limite (dev) : le realm n'a pas d'audience dédiée pour l'API — à configurer
              // en production (client scope « audience » Keycloak + ValidateAudience=true).
              o.TokenValidationParameters.ValidateAudience = false;
              // Piège : par défaut JwtBearer RENOMME les claims ("sub" → NameIdentifier,
              // "role" → ClaimTypes.Role) — FindFirst("sub") ne trouverait plus rien.
              o.MapInboundClaims = false;
              o.TokenValidationParameters.NameClaimType = "preferred_username";
              o.TokenValidationParameters.RoleClaimType = "role";
       });
builder.Services.AddAuthorization(o =>
       // Back-office : réservé au rôle realm Keycloak « stock-manager » (claim role du JWT).
       o.AddPolicy("CanManageStock", p => p.RequireRole("stock-manager")));

// Délai maximal des requêtes entrantes (concept Timeouts).
builder.Services.AddRequestTimeouts(o => o.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(15) });

// Limitation de débit (concept Rate Limits).
builder.Services.AddRateLimiter(o =>
{
       o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
       o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(
              partitionKey: ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
              factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) }));
       o.OnRejected = (ctx, _) =>
       {
              if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                     ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
              return ValueTask.CompletedTask;
       };
});

// OpenAPI (génération intégrée .NET 9+).
builder.Services.AddOpenApi();

var app = builder.Build();

// Migrations appliquées au démarrage (idempotent) : crée le schéma et le seed au premier
// lancement, y compris dans les conteneurs. Acceptable pour une instance unique ; en
// production multi-instances, préférer un job de migration dédié (bundle EF).
using (var scope = app.Services.CreateScope())
{
       var db = scope.ServiceProvider.GetRequiredService<ShopPulseDbContext>();
       db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
       app.UseExceptionHandler();
       app.UseHsts();
}
app.UseStatusCodePages();
app.UseSerilogRequestLogging(o =>
       o.EnrichDiagnosticContext = (diag, http) => diag.Set("TraceId", http.TraceIdentifier));
app.UseRequestTimeouts();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Sondes : /health/live = le processus répond (aucune dépendance) ;
// /health = disponibilité réelle (base + Keycloak), JSON détaillé.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
       Predicate = _ => false,
});
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
       ResponseWriter = WriteHealthJson,
});
app.MapPrometheusScrapingEndpoint();   // /metrics (format Prometheus)

if (app.Environment.IsDevelopment())
       app.MapOpenApi();                                   // /openapi/v1.json

app.MapHub<StockHub>("/hubs/stock");

var api = app.MapGroup("/api").RequireRateLimiting("api");

api.MapGet("/products", async (ShopPulse.Application.IProductService s, HttpContext ctx, CancellationToken ct) =>
{
       // Cachable : le navigateur 1 min, un éventuel CDN 5 min (module 5, concept CDN).
       ctx.Response.Headers.CacheControl = "public, max-age=60, s-maxage=300";
       ctx.Response.Headers.Vary = "Accept-Encoding";
       return Results.Ok(await s.GetAllAsync(ct));
})
.WithName("GetProducts")
.Produces<IReadOnlyList<Product>>(StatusCodes.Status200OK);

api.MapGet("/products/{id:int}", async (int id, ShopPulse.Application.IProductService s, HttpContext ctx, CancellationToken ct) =>
{
       var p = await s.GetByIdAsync(id, ct);
       if (p is null) return Results.NotFound();

       var etag = $"\"product-{p.Id}-{p.Stock}\"";
       if (ctx.Request.Headers.IfNoneMatch == etag)
              return Results.StatusCode(StatusCodes.Status304NotModified);   // revalidation

       ctx.Response.Headers.ETag = etag;
       ctx.Response.Headers.CacheControl = "no-cache";
       return Results.Ok(p);
})
.WithName("GetProduct")
.Produces<Product>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status404NotFound);

// Pagination, filtres et tri exécutés en SQL côté serveur : le client ne reçoit que sa page.
api.MapGet("/products/page", async (
       int? skip, int? take, string? search, string? category, string? sort,
       ShopPulse.Application.IProductService s, CancellationToken ct) =>
{
       if (!ShopPulse.Application.ProductSort.IsValid(sort))
              return (IResult)Results.BadRequest(new { error = $"Tri inconnu : « {sort} »." });
       var page = await s.GetPageAsync(Math.Max(0, skip ?? 0), Math.Clamp(take ?? 20, 1, 100),
              search, category, sort, ct);
       return Results.Ok(page);
})
.WithName("GetProductsPage")
.Produces<ShopPulse.Application.PagedResult<Product>>(StatusCodes.Status200OK);

api.MapGet("/categories", async (ShopPulse.Application.IProductService s, HttpContext ctx, CancellationToken ct) =>
{
       ctx.Response.Headers.CacheControl = "public, max-age=300, s-maxage=3600";
       return Results.Ok(await s.GetCategoriesAsync(ct));
})
.WithName("GetCategories")
.Produces<IReadOnlyList<string>>(StatusCodes.Status200OK);

// Paramètres commerciaux publics (livraison, TVA, devise) : le Web affiche le panier
// avec LA MÊME règle que celle appliquée à la création de la commande.
api.MapGet("/store/config", async (ShopPulse.Application.IStoreConfigProvider store,
       HttpContext ctx, CancellationToken ct) =>
{
       ctx.Response.Headers.CacheControl = "public, max-age=300";
       return Results.Ok(await store.GetAsync(ct));
})
.WithName("GetStoreConfig")
.Produces<ShopPulse.Application.StoreConfig>(StatusCodes.Status200OK);

api.MapPost("/stock", async (SetStockRequest req, EfProductService products, IStockPublisher publisher,
       HybridCache cache, CancellationToken ct) =>
{
       if (!await products.SetStockAsync(req.ProductId, req.Stock, ct))
              return (IResult)Results.NotFound();
       await cache.RemoveByTagAsync("catalog", ct);   // l'écriture invalide le cache catalogue
       await publisher.PublishAsync(req.ProductId, req.Stock);
       return Results.NoContent();
})
.WithName("SetStock")
.RequireAuthorization("CanManageStock")   // écriture réservée au back-office (P0)
.Produces(StatusCodes.Status204NoContent)
.ProducesValidationProblem();

// Décrément transactionnel (achat) : l'UPDATE conditionnel garantit le stock >= 0
// même sous concurrence ; 404 si le produit n'existe pas, 409 si le stock est insuffisant.
api.MapPost("/stock/decrement", async (DecrementStockRequest req, EfProductService products,
       IStockPublisher publisher, HybridCache cache, CancellationToken ct) =>
{
       if (req.Quantity <= 0)
              return (IResult)Results.BadRequest(new { error = "La quantité doit être positive." });
       var remaining = await products.TryDecrementStockAsync(req.ProductId, req.Quantity, ct);
       if (remaining is null)
              return (IResult)(await products.ExistsAsync(req.ProductId, ct)
                     ? Results.Conflict(new { error = "Stock insuffisant." })
                     : Results.NotFound());
       await cache.RemoveByTagAsync("catalog", ct);
       await publisher.PublishAsync(req.ProductId, remaining.Value);   // temps réel (module 6)
       return Results.NoContent();
})
.WithName("DecrementStock")
.RequireAuthorization("CanManageStock")   // écriture réservée au back-office (P0)
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status409Conflict)
.ProducesProblem(StatusCodes.Status404NotFound);

// Création de commande : prix recalculés serveur, transaction tout-ou-rien, idempotence
// par en-tête Idempotency-Key (rejeu → 200 avec la commande existante, jamais de doublon).
api.MapPost("/orders", async (PlaceOrderRequest req, HttpContext ctx,
       ShopPulse.Application.IOrderPlacer placer, ShopPulse.Application.IOrderEmailSender emails,
       IStockPublisher publisher, HybridCache cache, ILogger<Program> logger, CancellationToken ct) =>
{
       if (string.IsNullOrWhiteSpace(req.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(req.Email))
              return (IResult)Results.BadRequest(new { error = "Adresse email invalide." });
       if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Address)
              || string.IsNullOrWhiteSpace(req.PostalCode))
              return (IResult)Results.BadRequest(new { error = "Adresse de livraison incomplète." });

       var key = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
       // Client connecté ? Le sub du jeton lie la commande au compte (jamais fourni par le client).
       var userId = ctx.User.FindFirst("sub")?.Value;
       var placement = await placer.PlaceOrderAsync(new(req.Email, req.FullName, req.Address, req.PostalCode,
              [.. req.Lines.Select(l => new ShopPulse.Application.CartLineRequest(l.ProductId, l.Quantity))],
              key, userId), ct);

       switch (placement.Status)
       {
              case ShopPulse.Application.OrderPlacementStatus.Created:
                     await cache.RemoveByTagAsync("catalog", ct);
                     foreach (var (productId, newStock) in placement.StockUpdates)
                            await publisher.PublishAsync(productId, newStock);
                     // L'email de confirmation part via l'Outbox (écrit dans la transaction
                     // de la commande, envoyé par le worker — jamais perdu).
                     return Results.Created($"/api/orders/{placement.Order!.Id}", placement.Order);
              case ShopPulse.Application.OrderPlacementStatus.Replayed:
                     return Results.Ok(placement.Order);
              case ShopPulse.Application.OrderPlacementStatus.StockConflict:
                     return Results.Conflict(new { error = placement.Message });
              case ShopPulse.Application.OrderPlacementStatus.Invalid:
                     return Results.BadRequest(new { error = placement.Message });
              default:
                     return Results.Problem(placement.Message);
       }
})
.WithName("PlaceOrder")
.Produces<Order>(StatusCodes.Status201Created)
.Produces<Order>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status409Conflict)
.ProducesValidationProblem();

// Historique du client connecté (pages « Mon compte ») : exige un jeton bearer.
api.MapGet("/orders/me", async (HttpContext ctx, EfOrderService orders, CancellationToken ct) =>
{
       var userId = ctx.User.FindFirst("sub")?.Value;
       return userId is null
              ? Results.Unauthorized()
              : Results.Ok(await orders.GetForUserAsync(userId, ct));
})
.WithName("GetMyOrders")
.RequireAuthorization()
.Produces<IReadOnlyList<Order>>(StatusCodes.Status200OK);

// Consultation par identifiant. Contrôle d'accès (phase 4) :
// — commande liée à un compte → réservée à CE compte ; 404 (et non 403) sinon,
//   pour ne pas révéler l'existence de la commande ;
// — commande invitée (UserId nul) → accessible par lien (Guid v7 non devinable).
// Limite : un lien de suivi invité signé (id + email) serait la version durcie.
api.MapGet("/orders/{id:guid}", async (Guid id, HttpContext ctx, EfOrderService orders, CancellationToken ct) =>
{
       if (await orders.FindByIdAsync(id, ct) is not { } order)
              return Results.NotFound();
       if (order.UserId is not null && order.UserId != ctx.User.FindFirst("sub")?.Value)
              return Results.NotFound();
       return Results.Ok(order);
})
.WithName("GetOrder")
.Produces<Order>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status404NotFound);

// Suivi invité : lien signé HMAC envoyé dans l'email de confirmation — pas de session,
// pas d'énumération (signature en temps constant, 404 sinon).
api.MapGet("/orders/{id:guid}/track", async (Guid id, string? sig, HttpContext ctx,
       EfOrderService orders, CancellationToken ct) =>
{
       var key = ctx.RequestServices.GetRequiredService<IConfiguration>()["Store:TrackingKey"]
              ?? OrderTracking.DefaultDevKey;
       if (!OrderTracking.Validate(id, sig, key))
              return (IResult)Results.NotFound();
       return await orders.FindByIdAsync(id, ct) is { } order
              ? Results.Ok(order)
              : Results.NotFound();
})
.WithName("TrackOrder")
.Produces<Order>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status404NotFound);

// ---- Paiement (phase 6) ----

// Session de paiement d'une commande en attente : le montant vient de la commande
// persistée (recalculé serveur), l'idempotence Stripe = id de commande.
api.MapPost("/orders/{id:guid}/payment", async (Guid id, HttpContext ctx,
       EfOrderService orders, ShopPulse.Application.IPaymentGateway gateway, CancellationToken ct) =>
{
       if (await orders.FindByIdAsync(id, ct) is not { } order)
              return (IResult)Results.NotFound();
       // Même règle d'accès que GET /orders/{id} : commande liée → son compte uniquement.
       if (order.UserId is not null && order.UserId != ctx.User.FindFirst("sub")?.Value)
              return (IResult)Results.NotFound();
       if (order.Status != OrderStatus.Pending)
              return (IResult)Results.Conflict(new { error = "Cette commande n'attend plus de paiement." });

       var session = await gateway.CreatePaymentAsync(order, ct);
       await orders.SetPaymentIntentAsync(id, session.PaymentIntentId, ct);
       return Results.Ok(new PaymentSessionDto(
              session.PaymentIntentId, session.ClientSecret, session.PublishableKey, session.Live));
})
.WithName("CreatePaymentSession")
.Produces<PaymentSessionDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status409Conflict)
.ProducesProblem(StatusCodes.Status404NotFound);

// Webhook Stripe : SEULE source de vérité du paiement (jamais le retour navigateur).
// La signature est validée par le gateway ; la transition Pending → Paid est idempotente.
api.MapPost("/webhooks/stripe", async (HttpContext ctx,
       ShopPulse.Application.IPaymentGateway gateway, EfOrderService orders,
       ShopPulse.Application.IOrderEmailSender emails, ILogger<Program> logger, CancellationToken ct) =>
{
       using var reader = new StreamReader(ctx.Request.Body);
       var payload = await reader.ReadToEndAsync(ct);

       Guid? orderId;
       try
       {
              orderId = await gateway.HandleWebhookAsync(
                     payload, ctx.Request.Headers["Stripe-Signature"], ct);
       }
       catch (Exception ex)
       {
              logger.LogWarning(ex, "Webhook Stripe rejeté : signature invalide.");
              return (IResult)Results.BadRequest(new { error = "Signature invalide." });
       }
       if (orderId is null)
              return Results.Ok();   // événement non lié à un paiement : accusé, ignoré

       return await ConfirmPaidAsync(orderId.Value, orders, emails, logger, ct);
})
.WithName("StripeWebhook")
.Produces(StatusCodes.Status200OK)
.ProducesValidationProblem();

// Simulateur de développement : mappé UNIQUEMENT sans clés Stripe (gateway fake).
// Permet de tester toute la boucle commande → paiement → Paid sans compte Stripe.
if (string.IsNullOrEmpty(builder.Configuration["Stripe:SecretKey"]))
{
       api.MapPost("/orders/{id:guid}/payment/fake-confirm", async (Guid id, HttpContext ctx,
              EfOrderService orders, ShopPulse.Application.IOrderEmailSender emails,
              ILogger<Program> logger, CancellationToken ct) =>
       {
              if (await orders.FindByIdAsync(id, ct) is not { } order)
                     return (IResult)Results.NotFound();
              if (order.UserId is not null && order.UserId != ctx.User.FindFirst("sub")?.Value)
                     return (IResult)Results.NotFound();
              return await ConfirmPaidAsync(id, orders, emails, logger, ct);
       })
       .WithName("FakeConfirmPayment")
       .Produces(StatusCodes.Status200OK)
       .ProducesProblem(StatusCodes.Status404NotFound);
}

// ---- Back-office (rôle stock-manager, phase 5) ----
var admin = api.MapGroup("/admin").RequireAuthorization("CanManageStock");

admin.MapGet("/orders", async (string? status, int? page, EfOrderService orders, CancellationToken ct) =>
{
       OrderStatus? filter = null;
       if (!string.IsNullOrWhiteSpace(status))
       {
              if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
                     return (IResult)Results.BadRequest(new { error = $"Statut inconnu : « {status} »." });
              filter = parsed;
       }
       var current = Math.Max(1, page ?? 1);
       return Results.Ok(await orders.GetOrdersAsync(filter, (current - 1) * 25, 25, ct));
})
.WithName("AdminGetOrders")
.Produces<ShopPulse.Application.PagedResult<Order>>(StatusCodes.Status200OK);

admin.MapPut("/orders/{id:guid}/status", async (Guid id, SetOrderStatusRequest req,
       EfOrderService orders, ShopPulse.Application.IOrderEmailSender emails,
       ShopPulse.Application.IPaymentGateway gateway,
       IStockPublisher publisher, HybridCache cache,
       ILogger<Program> logger, CancellationToken ct) =>
{
       if (!Enum.TryParse<OrderStatus>(req.Status, ignoreCase: true, out var status))
              return (IResult)Results.BadRequest(new { error = $"Statut inconnu : « {req.Status} »." });

       // Annulation d'une commande PAYÉE : rembourser AVANT de changer le statut.
       // Si le remboursement échoue, on ne touche à rien (409, réessayable) —
       // jamais de commande annulée avec l'argent conservé sans action manuelle.
       if (status == OrderStatus.Cancelled
              && await orders.FindByIdAsync(id, ct) is { Status: OrderStatus.Paid } paid)
       {
              var refund = await gateway.RefundAsync(paid, ct);
              if (refund.Status == ShopPulse.Application.RefundStatus.Failed)
              {
                     logger.LogWarning("Remboursement refusé pour la commande {OrderId} : {Error}",
                            id, refund.Error);
                     return (IResult)Results.Conflict(new { error = $"Remboursement impossible : {refund.Error}" });
              }
              logger.LogInformation("Commande {OrderId} remboursée ({Result}).", id, refund.Status);
       }

       var result = await orders.SetStatusAsync(id, status, ct);
       if (result.Error is not null)
              return (IResult)Results.Conflict(new { error = result.Error });
       if (result.Order is null)
              return (IResult)Results.NotFound();

       // Restock (annulation) : invalider le cache et publier les nouveaux stocks (temps réel).
       if (result.StockUpdates.Count > 0)
       {
              await cache.RemoveByTagAsync("catalog", ct);
              foreach (var (productId, newStock) in result.StockUpdates)
                     await publisher.PublishAsync(productId, newStock);
       }

       // Email de statut : écrit en Outbox par SetStatusAsync, envoyé par le worker.
       return Results.Ok(result.Order);
})
.WithName("AdminSetOrderStatus")
.Produces<Order>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status409Conflict)
.ProducesProblem(StatusCodes.Status404NotFound);

admin.MapPost("/products", async (ShopPulse.Application.ProductUpsert req,
       EfProductService products, HybridCache cache, CancellationToken ct) =>
{
       var result = await products.CreateAsync(req, ct);
       if (result.Product is null)
              return (IResult)Results.BadRequest(new { error = result.Error });
       await cache.RemoveByTagAsync("catalog", ct);
       return Results.Created($"/api/products/{result.Product.Id}", result.Product);
})
.WithName("AdminCreateProduct")
.Produces<Product>(StatusCodes.Status201Created)
.ProducesValidationProblem();

admin.MapPut("/products/{id:int}", async (int id, ShopPulse.Application.ProductUpsert req,
       EfProductService products, HybridCache cache, CancellationToken ct) =>
{
       var result = await products.UpdateAsync(id, req, ct);
       if (result.Product is null)
              return (IResult)Results.BadRequest(new { error = result.Error });
       await cache.RemoveByTagAsync("catalog", ct);
       return Results.Ok(result.Product);
})
.WithName("AdminUpdateProduct")
.Produces<Product>(StatusCodes.Status200OK)
.ProducesValidationProblem();

app.Run();

// Réponse JSON détaillée de /health : statut global + état de chaque dépendance.
static async Task WriteHealthJson(HttpContext context,
       Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
{
       context.Response.ContentType = "application/json; charset=utf-8";
       await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
       {
              status = report.Status.ToString(),
              totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds),
              checks = report.Entries.Select(e => new
              {
                     name = e.Key,
                     status = e.Value.Status.ToString(),
                     durationMs = Math.Round(e.Value.Duration.TotalMilliseconds),
                     description = e.Value.Description,
              }),
       }));
}

// Confirmation d'un paiement : transition Pending → Paid idempotente + email client.
// Partagée par le webhook Stripe (prod) et le faux endpoint de dev.
static async Task<IResult> ConfirmPaidAsync(Guid orderId, EfOrderService orders,
       ShopPulse.Application.IOrderEmailSender emails,
       Microsoft.Extensions.Logging.ILogger logger, CancellationToken ct)
{
       if (await orders.FindByIdAsync(orderId, ct) is not { } order)
              return Results.NotFound();
       if (order.Status == OrderStatus.Paid)
              return Results.Ok(order);              // rejeu du webhook : rien à refaire

       var result = await orders.SetStatusAsync(orderId, OrderStatus.Paid, ct);
       if (result.Order is null)
              return result.Error is not null
                     ? Results.Conflict(new { error = result.Error })
                     : Results.NotFound();

       // Email de statut : écrit en Outbox par SetStatusAsync, envoyé par le worker.
       return Results.Ok(result.Order);
}

// Rend le Program des top-level statements visible pour WebApplicationFactory (tests d'intégration).
public partial class Program;
