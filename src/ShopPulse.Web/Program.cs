using Serilog;
using ShopPulse.Application;
using ShopPulse.Features.Account;
using ShopPulse.Features.Admin;
using ShopPulse.Features.Cart;
using ShopPulse.Features.Catalog;
using ShopPulse.Features.Checkout;
using ShopPulse.Infrastructure;
using ShopPulse.Modularity;
using ShopPulse.SharedKernel;
using ShopPulse.Web.Components;
using ShopPulse.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// Observabilité : logs structurés Serilog (mêmes conventions que l'API).
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ShopPulse.Web")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{Application}] {Message:lj}{NewLine}{Exception}"));

// Ne pas trahir la technologie : masquer l'en-tête Server.
builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);
builder.Services.AddHttpContextAccessor();   // nonce CSP dans App.razor

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Module 7 — Security : OIDC (code + PKCE), cookie de session, autorisation par politiques.
builder.Services.AddAuthentication(o =>
{
       o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
       o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(o =>
{
       o.Cookie.Name = "__Host-shoppulse";                  // lié à l'hôte, Secure, Path=/, sans Domain
       // Piège : le builder par défaut reprend le PathBase de la requête ("/fr" via le
       // middleware de locale) — or __Host- EXIGE Path=/ : le navigateur rejetterait le
       // cookie silencieusement (boucle de connexion infinie). On force Path=/.
       o.Cookie.Path = "/";
       o.Cookie.HttpOnly = true;                            // illisible par JavaScript (limite le vol par XSS)
       o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
       o.Cookie.SameSite = SameSiteMode.Lax;                // Strict casserait le ret OIDC depuis Keycloak
       o.ExpireTimeSpan = TimeSpan.FromMinutes(20);         // expiration d'inactivité (sliding)
       o.SlidingExpiration = true;
       o.Events.OnValidatePrincipal = async ctx =>          // expiration absolue
       {
              var issued = ctx.Properties.IssuedUtc;
              if (issued is not null && DateTimeOffset.UtcNow - issued > TimeSpan.FromHours(8))
              {
                     ctx.RejectPrincipal();
                     await ctx.HttpContext.SignOutAsync();
              }
       };
})
.AddOpenIdConnect(o =>
{
       o.Authority = builder.Configuration["Oidc:Authority"];
       // HTTPS exigé pour les métadonnées OIDC hors développement. Désactivable explicitement
       // (configuration uniquement) pour un IdP de dev en clair — jamais par défaut.
       o.RequireHttpsMetadata = builder.Configuration.GetValue("Oidc:RequireHttpsMetadata",
              !builder.Environment.IsDevelopment());
       // DNS split-horizon (déploiement en conteneurs) : les métadonnées sont chargées
       // via une adresse interne, mais l'émetteur du jeton reste l'URL publique.
       if (builder.Configuration["Oidc:ValidIssuer"] is { } validIssuer)
              o.TokenValidationParameters.ValidIssuer = validIssuer;
       o.ClientId = builder.Configuration["Oidc:ClientId"];
       o.ClientSecret = builder.Configuration["Oidc:ClientSecret"];   // user-secrets en dev, jamais dans le dépôt
       o.ResponseType = OpenIdConnectResponseType.Code;
       o.UsePkce = true;
       o.SaveTokens = true;                                 // jetons dans le cookie chiffré, jamais côté client
       o.GetClaimsFromUserInfoEndpoint = true;
       o.MapInboundClaims = false;
       o.Scope.Add("openid"); o.Scope.Add("profile"); o.Scope.Add("email");
       // Rôles realm Keycloak : avec MapInboundClaims=false, "role" et "preferred_username"
       // ne sont pas rebasculés vers les types Schema — on les désigne explicitement.
       o.TokenValidationParameters.RoleClaimType = "role";
       o.TokenValidationParameters.NameClaimType = "preferred_username";
       // Capture du jeton d'accès à la connexion : les circuits Blazor n'ont pas de
       // HttpContext — le UserTokenStore le rend disponible par sub (phase 4).
       o.Events = new OpenIdConnectEvents
       {
              OnTokenValidated = ctx =>
              {
                     var sub = ctx.Principal?.FindFirst("sub")?.Value;
                     var accessToken = ctx.TokenEndpointResponse?.AccessToken;
                     if (sub is not null && accessToken is not null)
                     {
                            var lifetime = int.TryParse(ctx.TokenEndpointResponse?.ExpiresIn, out var seconds)
                                   ? TimeSpan.FromSeconds(seconds)
                                   : TimeSpan.FromMinutes(25);
                            ctx.HttpContext.RequestServices.GetRequiredService<UserTokenStore>()
                                   .Set(sub, accessToken, ctx.TokenEndpointResponse?.RefreshToken, lifetime);
                     }
                     return Task.CompletedTask;
              }
       };
});

builder.Services.AddAuthorization(o =>
{
       o.AddPolicy("CanManageStock", p => p.RequireAuthenticatedUser().RequireRole("stock-manager"));
       o.FallbackPolicy = null;                             // le catalogue reste public
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingProvider>();

// Composition des modules : chaque feature enregistre SES services (frontières nettes).
IShopModule[] modules = [new CatalogModule(), new CartModule(), new CheckoutModule(), new AccountModule(), new AdminModule()];
foreach (var m in modules) m.ConfigureServices(builder.Services);
builder.Services.AddSingleton(new ModuleCatalog(modules));

// État et services transverses.
builder.Services.AddScoped<QueryClient>();
builder.Services.AddScoped<SwrCache>();
// Module 8 — i18n : IStringLocalizer + les cultures supportées.
builder.Services.AddLocalization();

// Health check : le Web est « Healthy » si l'API répond.
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri(new Uri(builder.Configuration["Api:BaseUrl"]!), "health"), name: "api");
// Annonces aux lecteurs d'écran (région live unique dans le layout).
builder.Services.AddScoped<ILiveAnnouncer, LiveAnnouncer>();
// Module 6 — Networking : l'interface parle à l'API via un HttpClient typé et résilient.
// Ne jamais faire new HttpClient() par appel : AddHttpClient gère pool et DNS.
builder.Services.AddHttpClient<IProductService, ProductApiClient>(c =>
              c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]!))
       .AddStandardResilienceHandler(o =>
       {
              o.Retry.MaxRetryAttempts = 3;
              o.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
              o.Retry.UseJitter = true;
              o.Retry.Delay = TimeSpan.FromMilliseconds(300);
              o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
              o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
       });
// Client nommé pour la file d'attente hors ligne (rejoue les écritures avec idempotence).
// Le handler propage le jeton d'accès de l'utilisateur courant vers l'API (phase 4).
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<UserTokenStore>();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddTransient<AccessTokenHandler>();
builder.Services.AddHttpClient("api", c =>
              c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]!))
       .AddHttpMessageHandler<AccessTokenHandler>();
builder.Services.AddScoped<IStockFeed, StockFeed>();  // une connexion SignalR par circuit
builder.Services.AddScoped<BrowserDb>();          // interop IndexedDB (file d'attente hors ligne)
builder.Services.AddScoped<OfflineQueue>();
builder.Services.AddScoped<IStockAdmin, StockAdminClient>();   // back-office stock (API, autorisé)
builder.Services.AddScoped<OrderApiClient>();                  // client HTTP des commandes (API)
builder.Services.AddScoped<IOrderPlacer>(sp => sp.GetRequiredService<OrderApiClient>());
builder.Services.AddScoped<IOrderHistory>(sp => sp.GetRequiredService<OrderApiClient>());
builder.Services.AddScoped<IPaymentClient>(sp => sp.GetRequiredService<OrderApiClient>());
builder.Services.AddScoped<IOrderTracking>(sp => sp.GetRequiredService<OrderApiClient>());
builder.Services.AddScoped<AdminApiClient>();                  // back-office (API, rôle stock-manager)
builder.Services.AddScoped<IOrderAdmin>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<IProductAdmin>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<IStoreConfigProvider, StoreConfigApiClient>();   // livraison/TVA (API)
builder.Services.AddSingleton<HtmlSanitizerService>();         // MarkupString n'est utilisé qu'avec lui
builder.Services.AddSingleton<CatalogNotifier>();   // bus sans état : SEUL Singleton métier
builder.Services.AddScoped<IFavoriteService, InMemoryFavoriteService>();
builder.Services.AddCascadingValue(sp => new ThemeState());
builder.Services.AddOutputCache(o =>
{
       // Clés de cache : la réponse varie selon la query string ET la langue (module 8),
       // avec un tag pour l'invalidation à la demande (règle d'or : toute promesse d'invalidation).
       o.AddPolicy("catalog", b => b
              .Expire(TimeSpan.FromMinutes(2))
              .SetVaryByQuery("category", "page", "q")
              .SetVaryByHeader("Accept-Language")
              .Tag("catalog"));
});

var app = builder.Build();

// Module 8 — locale routing : /fr/..., /en/..., /ar/... (préfixe → PathBase + culture).
// La racine redirige selon Accept-Language. Doit précéder le routage.
// La culture est posée directement par le middleware : UseRequestLocalization la
// réinitialiserait depuis ses propres providers (cookie/query/Accept-Language).
app.UseLocalePrefix();
// Le routage est sinon inséré automatiquement AVANT notre middleware (hôte minimal) :
// il faut le placer explicitement après la réécriture de préfixe.
app.UseRouting();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.UseSerilogRequestLogging(o =>
    o.EnrichDiagnosticContext = (diag, http) => diag.Set("TraceId", http.TraceIdentifier));

// Module 7 : en-têtes de sécurité + CSP (Report-Only) avec nonce par requête.
app.UseSecurityHeaders();
// Server-Timing : où va le temps serveur, lisible dans l'onglet Network des DevTools
app.Use(async (ctx, next) =>
{
       var sw = System.Diagnostics.Stopwatch.StartNew();
       ctx.Response.OnStarting(() =>
       {
              ctx.Response.Headers["Server-Timing"] = $"app;dur={sw.ElapsedMilliseconds}";
              // Pages avec données personnelles : jamais de cache partagé (module 5, concept 1)
              if (ctx.User.Identity?.IsAuthenticated == true)
                     ctx.Response.Headers.CacheControl = "no-store";
              return Task.CompletedTask;
       });
       await next();
});

app.UseOutputCache();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies([.. modules.Select(m => m.UiAssembly), typeof(ShopPulse.UI.Marker).Assembly]);

// Module 7 — connexion / déconnexion. returnUrl validé : anti-redirection ouverte.
// Sondes : /health/live = processus vivant ; /health = l'API répond (JSON détaillé).
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = WriteHealthJson,
});

app.MapGet("/login", (string? returnUrl, HttpContext ctx) =>
       Results.Challenge(
              new AuthenticationProperties { RedirectUri = LocalUrl(returnUrl) },
              [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapPost("/logout", (HttpContext ctx) =>
       Results.SignOut(
              new AuthenticationProperties { RedirectUri = "/" },
              [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
    .RequireAuthorization();

static string LocalUrl(string? url) =>
       !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") ? url : "/";

app.Run();

// Réponse JSON détaillée de /health (même format que l'API).
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
