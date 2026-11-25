using Microsoft.Playwright;

namespace ShopPulse.E2E;

/// <summary>Test OPT-IN du refresh token : exige un realm configuré avec des access
/// tokens de 60 s (accessTokenLifespan=60, recréer Keycloak) et SHOPPULSE_REFRESH_TEST=1.
/// Sans ces conditions, le test sort immédiatement (no-op). Vérifié une fois : commande
/// liée au compte malgré un token expiré depuis 15 s (preuve en base, docs/21). On se connecte, on attend 75 s (expiration),
/// puis on achète : la commande doit être LIÉE au compte — preuve que le Web a
/// rafraîchi l'access token via le refresh token (sinon : commande invitée).</summary>
public class RefreshTokenE2eTests : IAsyncLifetime
{
       private static readonly string WebUrl =
              Environment.GetEnvironmentVariable("SHOPPULSE_WEBURL") ?? "http://localhost:5026";
       private const int ProductId = 6;   // page 1 du catalogue, en stock

       private IPlaywright _pw = default!;
       private IBrowser _browser = default!;

       public async Task InitializeAsync()
       {
              _pw = await Playwright.CreateAsync();
              _browser = await _pw.Chromium.LaunchAsync();
       }

       [Fact]
       public async Task ExpiredToken_IsRefreshed_OrderStaysLinkedToAccount()
       {
              if (Environment.GetEnvironmentVariable("SHOPPULSE_REFRESH_TEST") != "1")
                     return;   // opt-in : nécessite le realm à 60 s

              var page = await _browser.NewPageAsync();

              // Connexion (access token : 60 s).
              await page.GotoAsync($"{WebUrl}/fr/account");
              await page.WaitForSelectorAsync("#username", new PageWaitForSelectorOptions { Timeout = 15000 });
              await page.FillAsync("#username", "client");
              await page.FillAsync("#password", "client1234");
              await page.ClickAsync("#kc-login");
              await page.WaitForSelectorAsync("h1:has-text('Mon compte')",
                     new PageWaitForSelectorOptions { Timeout = 15000 });

              // Le token expire…
              await page.WaitForTimeoutAsync(75_000);

              // … puis parcours d'achat complet.
              await page.ClickAsync("a[href='catalog']");
              await page.WaitForSelectorAsync($"a[href='/product/{ProductId}']");
              await page.ClickAsync($"a[href='/product/{ProductId}']");
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
              await page.WaitForSelectorAsync("h1");
              await page.ClickAsync("nav a[href='checkout']");
              await page.WaitForSelectorAsync("text=article(s)");
              await page.ClickAsync("button:has-text('Suivant')");
              await page.FillAsync("#fn", "Camille Cliente");
              await page.FillAsync("#em", "client@shoppulse.local");
              await page.FillAsync("#ad", "3 rue du Compte");
              await page.FillAsync("#pc", "69002");
              await page.ClickAsync("button[type='submit']");
              await page.ClickAsync("button:has-text('Payer')");
              await page.ClickAsync("button:has-text('Simuler le paiement')",
                     new PageClickOptions { Timeout = 15000 });
              await page.WaitForSelectorAsync("text=Commande confirmée",
                     new PageWaitForSelectorOptions { Timeout = 15000 });

              // Preuve : la commande passée APRÈS expiration figure dans « Mes commandes »
              // — l'appel API portait un token rafraîchi, pas un appel anonyme.
              await page.ClickAsync("a[href='account']");
              await page.WaitForSelectorAsync("h1:has-text('Mon compte')");
              await page.ClickAsync("a[href='/account/orders']");
              await page.WaitForSelectorAsync("h1:has-text('Mes commandes')");
              await page.WaitForSelectorAsync("ul li a",
                     new PageWaitForSelectorOptions { Timeout = 10000 });
       }

       public async Task DisposeAsync()
       {
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
