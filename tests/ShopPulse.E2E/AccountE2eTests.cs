using Microsoft.Playwright;
using System.Net;
using System.Text.Json;

namespace ShopPulse.E2E;

/// <summary>
/// Test E2E des comptes clients (phase 4) : connexion OIDC réelle via Keycloak,
/// commande passée en étant connecté (liée au sub), historique « Mon compte »,
/// et contrôle d'accès : la commande liée n'est plus lisible anonymement via l'API.
/// Exige : API (5299) + Web (5026) + Keycloak (8080) + db + Mailpit.
/// Utilisateur du realm de dev : client / client1234.
/// </summary>
[Collection("e2e")]
public class AccountE2eTests : IAsyncLifetime
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
       public async Task Login_checkout_and_account_history()
       {
              var page = await _browser.NewPageAsync();

              // 1. Page protégée → redirection vers Keycloak → connexion → retour.
              await page.GotoAsync($"{WebUrl}/fr/account");
              await page.WaitForSelectorAsync("#username",
                     new PageWaitForSelectorOptions { Timeout = 15000 });
              await page.FillAsync("#username", "client");
              await page.FillAsync("#password", "client1234");
              await page.ClickAsync("#kc-login");

              await page.WaitForSelectorAsync("h1:has-text('Mon compte')",
                     new PageWaitForSelectorOptions { Timeout = 30000 });   // post-login : redirections + jeton + circuit
              Assert.Contains("client", await page.TextContentAsync("dl"));

              // 2. Parcours d'achat connecté (navigation améliorée : même circuit).
              // Produit choisi EN STOCK au moment du test (les runs répétés vident le stock).
              var productId = await TestCatalog.InStockProductIdAsync(_http, ApiUrl, skip: 1);
              await E2eNavigation.ClickUntilAsync(page, "a[href='/catalog']", $"a[href='/product/{productId}']");
              await E2eNavigation.ClickUntilAsync(page, $"a[href='/product/{productId}']", "button.sp-btn--primary");
              // Clic pris en compte seulement une fois le circuit connecté : badge à 1, avec retry.
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
              await E2eNavigation.ClickUntilAsync(page, "a[href='/cart']", "h1");
              // lien du menu (le panier a aussi un CTA)
              await E2eNavigation.ClickUntilAsync(page, "nav a[href='checkout']", "text=article(s)");
              await E2eNavigation.ClickUntilAsync(page, "button:has-text('Suivant')", "#fn");
              await page.FillAsync("#fn", "Camille Cliente");
              await page.FillAsync("#em", "client@shoppulse.local");
              await page.FillAsync("#ad", "3 rue du Compte");
              await page.FillAsync("#pc", "69002");
              await E2eNavigation.ClickUntilAsync(page,
                     "button:has-text('Continuer')", "button:has-text('Payer')");
              // Session simulateur (dev sans clés Stripe) : confirmation dédiée.
              // « Payer » est idempotent (clé stable) : les reprises sont sûres.
              await E2eNavigation.ClickUntilAsync(page,
                     "button:has-text('Payer')", "button:has-text('Simuler le paiement')",
                     attempts: 3, timeoutMs: 15000);
              await E2eNavigation.ClickUntilAsync(page,
                     "button:has-text('Simuler le paiement')", "text=Commande confirmée",
                     attempts: 3, timeoutMs: 15000);

              // 3. Historique « Mes commandes » : la commande y figure, liée au compte.
              await E2eNavigation.ClickUntilAsync(page, "a[href='account']", "h1:has-text('Mon compte')");
              await E2eNavigation.ClickUntilAsync(page, "a[href='/account/orders']", "h1:has-text('Mes commandes')");
              var href = await page.GetAttributeAsync("ul li a", "href");
              Assert.NotNull(href);

              // 4. Contrôle d'accès API : cette commande liée n'est pas lisible anonymement.
              var orderId = href!.Split('/').Last();
              var anonymous = await _http.GetAsync($"{ApiUrl}/api/orders/{orderId}");
              Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);
       }

       public async Task DisposeAsync()
       {
              _http.Dispose();
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
