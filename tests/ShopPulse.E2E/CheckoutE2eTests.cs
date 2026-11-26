using Microsoft.Playwright;
using System.Net.Http.Json;
using System.Text.Json;

namespace ShopPulse.E2E;

/// <summary>
/// Test de bout en bout du tunnel de commande (phase 3) : ajout au panier, navigation
/// améliorée (même circuit Blazor, le panier survit), formulaire de livraison, paiement,
/// confirmation. Vérifie l'effet réel : stock décrémenté en base (via l'API) et email
/// de confirmation capturé par Mailpit.
/// Exige les serveurs de dev : API (5299) + Web (5026) + Mailpit (8025).
/// Nécessite les navigateurs Playwright : pwsh bin/Debug/net10.0/playwright.ps1 install chromium
/// </summary>
[Collection("e2e")]
public class CheckoutE2eTests : IAsyncLifetime
{
       private static readonly string WebUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_WEBURL") ?? "http://localhost:5026";
       private static readonly string ApiUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_APIURL") ?? "http://localhost:5299";
       private static readonly string MailpitUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_MAILPITURL") ?? "http://localhost:8025";

       private IPlaywright _pw = default!;
       private IBrowser _browser = default!;
       private readonly HttpClient _http = new();
       private int _productId;   // choisi en stock au début du test (les runs vident le stock)

       public async Task InitializeAsync()
       {
              _pw = await Playwright.CreateAsync();
              _browser = await _pw.Chromium.LaunchAsync();
       }

       [Fact]
       public async Task Checkout_places_order_decrements_stock_and_sends_email()
       {
              _productId = await TestCatalog.InStockProductIdAsync(_http, ApiUrl, skip: 0);
              var stockBefore = await GetStockAsync();

              var page = await _browser.NewPageAsync();
              await page.GotoAsync($"{WebUrl}/fr/product/{_productId}");
              await page.WaitForSelectorAsync("button.sp-btn--primary");

              // Le clic n'est pris en compte qu'une fois le circuit SignalR connecté :
              // cliquer puis attendre le badge panier à 1, avec nouvel essai si besoin.
              for (var attempt = 0; attempt < 4; attempt++)
              {
                     await page.ClickAsync("button.sp-btn--primary");
                     try
                     {
                            await page.WaitForSelectorAsync(".cart-badge:has-text('1')",
                                   new PageWaitForSelectorOptions { Timeout = 4000 });
                            break;
                     }
                     catch (TimeoutException) when (attempt < 3) { /* circuit pas prêt : on reclique */ }
              }

              // Navigation améliorée (liens internes) : même circuit → le panier survit.
              await E2eNavigation.ClickUntilAsync(page, "a[href='/cart']", "h1");
              // lien du menu (le panier a aussi un CTA)
              await E2eNavigation.ClickUntilAsync(page, "nav a[href='checkout']", "text=article(s)");

              await E2eNavigation.ClickUntilAsync(page, "button:has-text('Suivant')", "#fn");
              await page.FillAsync("#fn", "Test E2E");
              await page.FillAsync("#em", "e2e@example.fr");
              await page.FillAsync("#ad", "1 rue du Test");
              await page.FillAsync("#pc", "69001");
              await E2eNavigation.ClickUntilAsync(page,
                     "button:has-text('Continuer')", "button:has-text('Payer')");

              // « Payer » crée la commande (idempotente — la clé est stable pendant les
              // reprises) puis ouvre la session : en dev (sans clés Stripe), le simulateur
              // affiche un bouton de confirmation dédié. Clics robustifiés (E2eNavigation).
              await E2eNavigation.ClickUntilAsync(page,
                     "button:has-text('Payer')", "button:has-text('Simuler le paiement')",
                     attempts: 3, timeoutMs: 15000);
              await E2eNavigation.ClickUntilAsync(page,
                     "button:has-text('Simuler le paiement')", "text=Commande confirmée",
                     attempts: 3, timeoutMs: 15000);

              // CTA WhatsApp de la confirmation (docs/31) : le message pré-rempli porte
              // la référence de la commande — lien wa.me avec le numéro configuré.
              var whatsappHref = await page.GetAttributeAsync(".summary-box a.whatsapp-link", "href");
              Assert.NotNull(whatsappHref);
              Assert.Contains("wa.me/212700222673", whatsappHref);
              Assert.Contains("commande", whatsappHref);

              // Effets réels : stock décrémenté d'exactement 1 ; emails capturés par Mailpit
              // (envoyés de façon asynchrone par le worker Outbox → sondage jusqu'à 20 s).
              Assert.Equal(stockBefore - 1, await GetStockAsync());

              var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
              var count = 0;
              while (DateTime.UtcNow < deadline)
              {
                     var mailpit = await _http.GetFromJsonAsync<JsonElement>(
                            $"{MailpitUrl}/api/v1/search?query=to:e2e@example.fr");
                     count = mailpit.GetProperty("messages_count").GetInt32();
                     if (count >= 2) break;   // confirmation + statut « payée »
                     await Task.Delay(1000);
              }
              Assert.True(count >= 2, $"Emails de confirmation et de statut attendus dans Mailpit ({count} reçu).");
       }

       private async Task<int> GetStockAsync()
       {
              var product = await _http.GetFromJsonAsync<JsonElement>($"{ApiUrl}/api/products/{_productId}");
              return product.GetProperty("stock").GetInt32();
       }

       public async Task DisposeAsync()
       {
              _http.Dispose();
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
