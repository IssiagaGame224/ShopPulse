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
public class AccountE2eTests : IAsyncLifetime
{
       private const string WebUrl = "http://localhost:5026";
       private const string ApiUrl = "http://localhost:5299";
       private const int ProductId = 9;   // produit en stock (les ids multiples de 7 sont à 0)

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
                     new PageWaitForSelectorOptions { Timeout = 15000 });
              Assert.Contains("client", await page.TextContentAsync("dl"));

              // 2. Parcours d'achat connecté (navigation améliorée : même circuit).
              await page.ClickAsync("a[href='catalog']");
              await page.WaitForSelectorAsync($"a[href='/product/{ProductId}']");
              await page.ClickAsync($"a[href='/product/{ProductId}']");
              await page.WaitForSelectorAsync("button.sp-btn--primary");
              await page.WaitForTimeoutAsync(1200);          // circuit SignalR
              await page.ClickAsync("button.sp-btn--primary");
              await page.WaitForTimeoutAsync(300);
              await page.ClickAsync("a[href='/cart']");
              await page.WaitForSelectorAsync("h1");
              await page.ClickAsync("nav a[href='checkout']");   // lien du menu (le panier a aussi un CTA)
              await page.WaitForSelectorAsync("text=article(s)");
              await page.ClickAsync("button:has-text('Suivant')");
              await page.FillAsync("#fn", "Camille Cliente");
              await page.FillAsync("#em", "client@shoppulse.local");
              await page.FillAsync("#ad", "3 rue du Compte");
              await page.FillAsync("#pc", "69002");
              await page.ClickAsync("button[type='submit']");
              await page.ClickAsync("button:has-text('Payer')");
              // Session simulateur (dev sans clés Stripe) : confirmation dédiée.
              await page.ClickAsync("button:has-text('Simuler le paiement')",
                     new PageClickOptions { Timeout = 15000 });
              await page.WaitForSelectorAsync("text=Commande confirmée",
                     new PageWaitForSelectorOptions { Timeout = 15000 });

              // 3. Historique « Mes commandes » : la commande y figure, liée au compte.
              await page.ClickAsync("a[href='account']");
              await page.WaitForSelectorAsync("h1:has-text('Mon compte')");
              await page.ClickAsync("a[href='/account/orders']");
              await page.WaitForSelectorAsync("h1:has-text('Mes commandes')");
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
