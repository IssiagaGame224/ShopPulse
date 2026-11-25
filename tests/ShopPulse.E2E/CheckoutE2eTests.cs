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
public class CheckoutE2eTests : IAsyncLifetime
{
       private static readonly string WebUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_WEBURL") ?? "http://localhost:5026";
       private static readonly string ApiUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_APIURL") ?? "http://localhost:5299";
       private static readonly string MailpitUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_MAILPITURL") ?? "http://localhost:8025";
       private const int ProductId = 2;

       private IPlaywright _pw = default!;
       private IBrowser _browser = default!;
       private readonly HttpClient _http = new();

       public async Task InitializeAsync()
       {
              _pw = await Playwright.CreateAsync();
              _browser = await _pw.Chromium.LaunchAsync();
       }

       [Fact]
       public async Task Checkout_places_order_decrements_stock_and_sends_email()
       {
              var stockBefore = await GetStockAsync();

              var page = await _browser.NewPageAsync();
              await page.GotoAsync($"{WebUrl}/fr/product/{ProductId}");
              await page.WaitForSelectorAsync("button.sp-btn--primary");
              await page.WaitForTimeoutAsync(1500);        // laisser le circuit SignalR se connecter

              await page.ClickAsync("button.sp-btn--primary");   // Ajouter au panier
              await page.WaitForTimeoutAsync(300);

              // Navigation améliorée (liens internes) : même circuit → le panier survit.
              await page.ClickAsync("a[href='/cart']");
              await page.WaitForSelectorAsync("h1");
              await page.ClickAsync("nav a[href='checkout']");   // lien du menu (le panier a aussi un CTA)
              await page.WaitForSelectorAsync("text=article(s)");

              await page.ClickAsync("button:has-text('Suivant')");
              await page.FillAsync("#fn", "Test E2E");
              await page.FillAsync("#em", "e2e@example.fr");
              await page.FillAsync("#ad", "1 rue du Test");
              await page.FillAsync("#pc", "69001");
              await page.ClickAsync("button[type='submit']");

              // « Payer » crée la commande puis ouvre la session : en dev (sans clés
              // Stripe), le simulateur affiche un bouton de confirmation dédié.
              await page.ClickAsync("button:has-text('Payer')");
              await page.ClickAsync("button:has-text('Simuler le paiement')",
                     new PageClickOptions { Timeout = 15000 });
              await page.WaitForSelectorAsync("text=Commande confirmée",
                     new PageWaitForSelectorOptions { Timeout = 15000 });

              // Effets réels : stock décrémenté d'exactement 1, email capturé par Mailpit,
              // email de statut « payée » envoyé (confirmation simulée = webhook en dev).
              Assert.Equal(stockBefore - 1, await GetStockAsync());

              var mailpit = await _http.GetFromJsonAsync<JsonElement>(
                     $"{MailpitUrl}/api/v1/search?query=to:e2e@example.fr");
              Assert.True(mailpit.GetProperty("messages_count").GetInt32() >= 2,
                     "Emails de confirmation et de statut attendus dans Mailpit.");
       }

       private async Task<int> GetStockAsync()
       {
              var product = await _http.GetFromJsonAsync<JsonElement>($"{ApiUrl}/api/products/{ProductId}");
              return product.GetProperty("stock").GetInt32();
       }

       public async Task DisposeAsync()
       {
              _http.Dispose();
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
