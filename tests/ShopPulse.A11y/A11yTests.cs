using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Xunit;

namespace ShopPulse.A11y;

/// <summary>
/// Exercice 2 (module 8) : axe-core détecte 30-40 % des problèmes d'accessibilité
/// (contraste, noms manquants, rôles invalides). Il ne remplace ni le clavier ni le
/// lecteur d'écran. Exige les serveurs de dev : API (5299) + Web (5026) —
/// cible configurable via SHOPPULSE_WEBURL.
/// Nécessite les navigateurs Playwright : pwsh bin/Debug/net10.0/playwright.ps1 install chromium
/// </summary>
public class A11yTests : IAsyncLifetime
{
       private static readonly string BaseUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_WEBURL") ?? "http://localhost:5026";
       private IPlaywright _pw = default!;
       private IBrowser _browser = default!;

       public async Task InitializeAsync()
       {
              _pw = await Playwright.CreateAsync();
              _browser = await _pw.Chromium.LaunchAsync();
       }

       [Theory]
       [InlineData("/fr")]              // accueil : carrousel des gammes + CTA WhatsApp (docs/31)
       [InlineData("/fr/catalog")]
       [InlineData("/fr/product/1")]
       [InlineData("/fr/cart")]
       [InlineData("/en/catalog")]
       [InlineData("/ar/catalog")]      // RTL : le miroir de la mise en page est aussi testé
       public async Task Page_has_no_wcag_violations(string path)
       {
              var page = await _browser.NewPageAsync();
              await page.GotoAsync(BaseUrl + path);

              await page.WaitForSelectorAsync("h1");      // attendre la fin du streaming

              var results = await page.RunAxe(new AxeRunOptions
              {
                     RunOnly = new RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"] }
              });

              Assert.Empty(results.Violations.Select(v => $"{v.Id}: {v.Help}"));
       }

       public async Task DisposeAsync()
       {
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
