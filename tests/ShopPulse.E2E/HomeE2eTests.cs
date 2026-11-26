using Microsoft.Playwright;

namespace ShopPulse.E2E;

/// <summary>Test E2E de l'accueil refaite (docs/31) : carrousel des gammes
/// (navigation par flèche, indicateurs, bouton pause accessible) et CTA WhatsApp
/// flottant (lien wa.me avec le numéro configuré, nouvelle fenêtre).
/// Exige : API + Web + db.</summary>
[Collection("e2e")]
public class HomeE2eTests : IAsyncLifetime
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
       public async Task Home_shows_animated_hero_carousel_and_whatsapp_cta()
       {
              var page = await _browser.NewPageAsync();
              await page.GotoAsync($"{WebUrl}/fr");
              await page.WaitForSelectorAsync(".hero-slider");

              // Plusieurs diapositives (promos + gammes), une seule visible.
              var slideCount = await page.Locator(".hero-slide").CountAsync();
              Assert.True(slideCount >= 2, $"Au moins 2 diapositives attendues ({slideCount}).");
              Assert.Equal(1, await page.Locator(".hero-slide.is-active").CountAsync());

              // D'ABORD la pause (WCAG 2.2.2 — contenu animé arrêtable) : le clic n'est
              // pris en compte qu'une fois le circuit connecté → boucle jusqu'au basculement.
              // Cela prouve l'interactivité ET fige la rotation pour la suite du test.
              for (var attempt = 0; attempt < 4; attempt++)
              {
                     await page.ClickAsync(".hero-pause");
                     try
                     {
                            await page.WaitForSelectorAsync(".hero-pause[aria-pressed='true']",
                                   new PageWaitForSelectorOptions { Timeout = 4000 });
                            break;
                     }
                     catch (TimeoutException) when (attempt < 3) { }
              }
              Assert.Equal("true", await page.GetAttributeAsync(".hero-pause", "aria-pressed"));

              // Rotation figée : la flèche « suivante » active la 2e diapositive, de façon
              // déterministe (une seule visible à la fois).
              await page.ClickAsync("button[aria-label='Diapositive suivante']");
              await page.WaitForSelectorAsync(".hero-dot:nth-child(2)[aria-selected='true']",
                     new PageWaitForSelectorOptions { Timeout = 5000 });
              Assert.Equal(1, await page.Locator(".hero-slide.is-active").CountAsync());

              // CTA WhatsApp : lien wa.me avec le numéro configuré, nouvelle fenêtre sécurisée.
              var href = await page.GetAttributeAsync("a.whatsapp-cta", "href");
              Assert.Contains("wa.me/212700222673", href);
              Assert.Equal("_blank", await page.GetAttributeAsync("a.whatsapp-cta", "target"));
              Assert.Contains("noopener", await page.GetAttributeAsync("a.whatsapp-cta", "rel"));
              Assert.NotNull(await page.GetAttributeAsync("a.whatsapp-cta", "aria-label"));
       }

       public async Task DisposeAsync()
       {
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
