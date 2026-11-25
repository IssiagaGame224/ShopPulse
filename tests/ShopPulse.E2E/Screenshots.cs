using Microsoft.Playwright;

namespace ShopPulse.E2E;

/// <summary>Capture d'écran des pages refondues (vérification visuelle manuelle, phase design).
/// À exécuter ponctuellement : dotnet test --filter Screenshots</summary>
public class Screenshots : IAsyncLifetime
{
       private static readonly string WebUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_WEBURL") ?? "http://localhost:5026";

       private IPlaywright _pw = default!;
       private IBrowser _browser = default!;

       public async Task InitializeAsync()
       {
              _pw = await Playwright.CreateAsync();
              _browser = await _pw.Chromium.LaunchAsync();
       }

       [Fact]
       public async Task Capture_redesigned_pages()
       {
              var dir = Path.Combine(Path.GetTempPath(), "shoppulse-shots");
              Directory.CreateDirectory(dir);
              var page = await _browser.NewPageAsync(new BrowserNewPageOptions
              {
                     ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
              });

              await page.GotoAsync($"{WebUrl}/fr/");
              await page.WaitForSelectorAsync("h2");
              await page.WaitForTimeoutAsync(800);
              await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(dir, "home.png"), FullPage = true });

              await page.GotoAsync($"{WebUrl}/fr/product/5");
              await page.WaitForSelectorAsync(".product-hero");
              await page.WaitForTimeoutAsync(800);
              await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(dir, "product.png"), FullPage = true });

              await page.GotoAsync($"{WebUrl}/fr/catalog");
              await page.WaitForSelectorAsync(".card");
              await page.WaitForTimeoutAsync(800);
              await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(dir, "catalog.png"), FullPage = true });

              // Panier avec un article (ajout via la fiche produit)
              await page.GotoAsync($"{WebUrl}/fr/product/2");
              await page.WaitForSelectorAsync("button.sp-btn--primary");
              await page.WaitForTimeoutAsync(1500);
              await page.ClickAsync("button.sp-btn--primary");
              await page.WaitForTimeoutAsync(400);
              await page.ClickAsync("a[href='/cart']");
              await page.WaitForSelectorAsync(".cart-summary");
              await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(dir, "cart.png"), FullPage = true });
       }

       public async Task DisposeAsync()
       {
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
