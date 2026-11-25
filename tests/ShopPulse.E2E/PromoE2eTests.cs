using Microsoft.Playwright;

namespace ShopPulse.E2E;

/// <summary>Test E2E des codes promo (docs/29) : ajout au panier, application du code
/// semé BIENVENUE10 dans le panier, ligne de remise et total réduit affichés.
/// La remise réelle est re-calculée côté serveur à la commande (voir PromoTests).
/// Exige : API + Web + db (code semé par la migration).</summary>
[Collection("e2e")]
public class PromoE2eTests : IAsyncLifetime
{
       private static readonly string WebUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_WEBURL") ?? "http://localhost:5026";
       private static readonly string ApiUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_APIURL") ?? "http://localhost:5299";

       private IPlaywright _pw = default!;
       private IBrowser _browser = default!;
       private readonly HttpClient _http = new();

       public async Task InitializeAsync()
       {
              _pw = await Playwright.CreateAsync();
              _browser = await _pw.Chromium.LaunchAsync();
       }

       [Fact]
       public async Task Cart_applies_promo_code_and_shows_discount()
       {
              var productId = await TestCatalog.InStockProductIdAsync(_http, ApiUrl, skip: 3);

              var page = await _browser.NewPageAsync();
              await page.GotoAsync($"{WebUrl}/fr/product/{productId}");
              await page.WaitForSelectorAsync("button.sp-btn--primary");
              for (var attempt = 0; attempt < 4; attempt++)
              {
                     await page.ClickAsync("button.sp-btn--primary");
                     try
                     {
                            await page.WaitForSelectorAsync(".cart-badge:has-text('1')",
                                   new PageWaitForSelectorOptions { Timeout = 4000 });
                            break;
                     }
                     catch (TimeoutException) when (attempt < 3) { }
              }

              await page.ClickAsync("a[href='/cart']");
              await page.WaitForSelectorAsync(".cart-summary");

              // Appliquer le code : la ligne de remise apparaît, le total baisse.
              var totalBefore = await page.TextContentAsync(".cart-summary .total dd");
              await page.FillAsync("#promo-code", "BIENVENUE10");
              // Le bouton vit dans un îlot interactif : recliquer tant que le circuit n'est pas prêt.
              for (var attempt = 0; attempt < 4; attempt++)
              {
                     await page.ClickAsync("button:has-text('Appliquer')");
                     try
                     {
                            await page.WaitForSelectorAsync(".cart-summary .discount",
                                   new PageWaitForSelectorOptions { Timeout = 5000 });
                            break;
                     }
                     catch (TimeoutException) when (attempt < 3)
                     {
                            await page.FillAsync("#promo-code", "BIENVENUE10");
                     }
              }

              var discount = await page.TextContentAsync(".cart-summary .discount dd");
              Assert.StartsWith("−", discount);
              var totalAfter = await page.TextContentAsync(".cart-summary .total dd");
              Assert.NotEqual(totalBefore, totalAfter);

              // Le code appliqué est affiché avec son bouton « Retirer ».
              var applied = await page.TextContentAsync(".promo-applied");
              Assert.Contains("BIENVENUE10", applied);
       }

       public async Task DisposeAsync()
       {
              _http.Dispose();
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
