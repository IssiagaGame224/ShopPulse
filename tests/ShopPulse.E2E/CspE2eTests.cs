using Microsoft.Playwright;

namespace ShopPulse.E2E;

/// <summary>
/// Garde-fou de la CSP (docs/24) : style-src est passé de 'unsafe-inline' à 'self' —
/// l'application n'a plus aucun style inline. Deux vérifications :
/// 1. l'en-tête CSP servi ne contient plus 'unsafe-inline' dans style-src ;
/// 2. en navigateur réel, aucune violation CSP n'est rapportée en console sur les
///    pages principales (un style ou script bloqué serait journalisé « Refused to… »).
/// Exige les serveurs de dev : API (5299) + Web (5026).
/// </summary>
[Collection("e2e")]
public class CspE2eTests : IAsyncLifetime
{
       private static readonly string WebUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_WEBURL") ?? "http://localhost:5026";

       private static readonly string[] Pages =
              ["/fr", "/fr/catalog", "/fr/product/2", "/fr/cart", "/fr/checkout", "/ar"];

       private IPlaywright _pw = default!;
       private IBrowser _browser = default!;
       private readonly HttpClient _http = new();

       public async Task InitializeAsync()
       {
              _pw = await Playwright.CreateAsync();
              _browser = await _pw.Chromium.LaunchAsync();
       }

       [Fact]
       public async Task Csp_header_has_no_unsafe_inline_in_style_src()
       {
              using var response = await _http.GetAsync($"{WebUrl}/fr");
              // Plusieurs en-têtes CSP peuvent coexister (.NET ajoute frame-ancestors 'self'
              // sur les réponses Blazor) : ils se combinent par intersection — on les fusionne.
              var csp = string.Join("; ", response.Headers.GetValues("Content-Security-Policy"));

              var styleSrc = csp.Split(';').Select(d => d.Trim())
                     .Single(d => d.StartsWith("style-src", StringComparison.Ordinal));
              Assert.DoesNotContain("unsafe-inline", styleSrc);
              Assert.Contains("'self'", styleSrc);
       }

       [Fact]
       public async Task No_csp_violation_on_main_pages()
       {
              var violations = new List<string>();
              var page = await _browser.NewPageAsync();
              page.Console += (_, msg) =>
              {
                     if (msg.Type == "error" &&
                         (msg.Text.Contains("Content Security Policy") || msg.Text.Contains("Refused to")))
                            violations.Add(msg.Text);
              };

              foreach (var path in Pages)
              {
                     await page.GotoAsync($"{WebUrl}{path}");
                     // Laisser le circuit SignalR se connecter et l'interop JS s'exécuter :
                     // c'est là qu'une ressource bloquée se manifesterait.
                     await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
              }

              Assert.Empty(violations);
       }

       public async Task DisposeAsync()
       {
              await _browser.CloseAsync();
              _pw.Dispose();
              _http.Dispose();
       }
}
