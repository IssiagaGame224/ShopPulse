using Microsoft.Playwright;

namespace ShopPulse.E2E;

/// <summary>Garde-fou responsive (docs/33) : à 375 px de large (petit téléphone),
/// aucune page clé ne doit défiler horizontalement — panier rempli et tables
/// d'administration incluses (les deux débordaient avant ce chantier).
/// La mesure est faite dans la page : scrollWidth ≤ clientWidth (+1 px de tolérance).</summary>
[Collection("e2e")]
public class MobileResponsiveE2eTests : IAsyncLifetime
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

       private static Task<int> HorizontalOverflowAsync(IPage page) =>
              page.EvaluateAsync<int>(
                     "Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth)");

       private static async Task AssertNoOverflowAsync(IPage page, string label)
       {
              var overflow = await HorizontalOverflowAsync(page);
              Assert.True(overflow <= 1, $"{label} : débordement horizontal de {overflow}px à 375px de large.");
       }

       [Fact]
       public async Task Key_pages_do_not_scroll_horizontally_at_375px()
       {
              var page = await _browser.NewPageAsync(new BrowserNewPageOptions
              {
                     ViewportSize = new ViewportSize { Width = 375, Height = 812 }
              });

              // Pages publiques.
              foreach (var path in new[] { "/fr", "/fr/catalog" })
              {
                     await page.GotoAsync($"{WebUrl}{path}");
                     await page.WaitForSelectorAsync("h1");
                     await AssertNoOverflowAsync(page, path);
              }

              // Fiche produit + ajout panier (le panier rempli débordait).
              var productId = await TestCatalog.InStockProductIdAsync(_http, ApiUrl, skip: 4);
              await page.GotoAsync($"{WebUrl}/fr/product/{productId}");
              await page.WaitForSelectorAsync("button.sp-btn--primary");
              await AssertNoOverflowAsync(page, "fiche produit");
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

              await page.GotoAsync($"{WebUrl}/fr/cart");
              await page.WaitForSelectorAsync(".cart-summary");
              await AssertNoOverflowAsync(page, "panier rempli");

              await page.GotoAsync($"{WebUrl}/fr/checkout");
              await page.WaitForSelectorAsync("h2:has-text('Votre panier')");
              await AssertNoOverflowAsync(page, "tunnel (récapitulatif)");
              await page.ClickAsync("button:has-text('Suivant')");
              await page.WaitForSelectorAsync("#fn");
              await AssertNoOverflowAsync(page, "tunnel (livraison)");

              // Back-office : les tables débordaient de ~280 px — elles défilent
              // désormais dans leur conteneur .table-scroll.
              await page.GotoAsync($"{WebUrl}/fr/admin/orders");
              await page.WaitForSelectorAsync("#username",
                     new PageWaitForSelectorOptions { Timeout = 15000 });
              await page.FillAsync("#username", "demo");
              await page.FillAsync("#password", "demo1234");
              await page.ClickAsync("#kc-login");
              await page.WaitForSelectorAsync("h1:has-text('Commandes')",
                     new PageWaitForSelectorOptions { Timeout = 30000 });   // post-login : redirections + jeton + circuit
              await AssertNoOverflowAsync(page, "admin commandes");

              await page.GotoAsync($"{WebUrl}/fr/admin/products");
              await page.WaitForSelectorAsync(".table-scroll table");
              await AssertNoOverflowAsync(page, "admin produits");
       }

       public async Task DisposeAsync()
       {
              _http.Dispose();
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
