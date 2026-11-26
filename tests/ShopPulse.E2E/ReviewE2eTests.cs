using Microsoft.Playwright;

namespace ShopPulse.E2E;

/// <summary>Test E2E des avis clients (docs/28) : connexion, fiche produit,
/// soumission d'un avis via le formulaire, affichage immédiat dans la liste
/// avec la moyenne mise à jour. Exige : API + Web + Keycloak + db.</summary>
[Collection("e2e")]
public class ReviewE2eTests : IAsyncLifetime
{
       private static readonly string WebUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_WEBURL") ?? "http://localhost:5026";
       private const int ProductId = 5;   // les avis ne consomment pas de stock

       private IPlaywright _pw = default!;
       private IBrowser _browser = default!;

       public async Task InitializeAsync()
       {
              _pw = await Playwright.CreateAsync();
              _browser = await _pw.Chromium.LaunchAsync();
       }

       [Fact]
       public async Task Logged_in_customer_submits_a_review()
       {
              var page = await _browser.NewPageAsync();

              // Connexion via une page protégée (même parcours que AccountE2eTests).
              await page.GotoAsync($"{WebUrl}/fr/account");
              await page.WaitForSelectorAsync("#username",
                     new PageWaitForSelectorOptions { Timeout = 15000 });
              await page.FillAsync("#username", "client");
              await page.FillAsync("#password", "client1234");
              await page.ClickAsync("#kc-login");
              await page.WaitForSelectorAsync("h1:has-text('Mon compte')",
                     new PageWaitForSelectorOptions { Timeout = 30000 });   // post-login : redirections + jeton + circuit

              // Fiche produit : section avis, formulaire visible car connecté.
              await page.GotoAsync($"{WebUrl}/fr/product/{ProductId}");
              await page.WaitForSelectorAsync("h2:has-text('Avis clients')");
              await page.WaitForSelectorAsync(".review-form", new PageWaitForSelectorOptions { Timeout = 15000 });

              // Note 4 (clic sur la 4e radio) + commentaire horodaté (upsert : le texte change à chaque run).
              await page.ClickAsync(".review-rating label:nth-child(4)");
              var comment = $"Avis E2E du {DateTime.Now:HH:mm:ss} — très satisfait.";
              // Le formulaire est visible dès le prerender : tant que le circuit n'est pas
              // connecté, la soumission ne fait rien — ressaisir et recliquer jusqu'à
              // l'apparition de l'avis (même stratégie que le badge panier).
              for (var attempt = 0; attempt < 4; attempt++)
              {
                     await page.FillAsync("#review-comment", comment);
                     await page.ClickAsync("button:has-text('Publier mon avis')");
                     try
                     {
                            await page.WaitForSelectorAsync($".review-list li:has-text(\"{comment}\")",
                                   new PageWaitForSelectorOptions { Timeout = 5000 });
                            break;
                     }
                     catch (TimeoutException) when (attempt < 3) { /* circuit pas prêt : on resoumet */ }
              }
              var entry = await page.TextContentAsync($".review-list li:has-text(\"{comment}\")");
              Assert.Contains("client", entry);
              Assert.Contains("(votre avis)", entry);

              // La moyenne s'affiche dans le résumé de la section.
              var summary = await page.TextContentAsync(".reviews-summary");
              Assert.Contains("/5", summary);
       }

       public async Task DisposeAsync()
       {
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
