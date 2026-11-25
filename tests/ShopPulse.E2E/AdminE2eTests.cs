using Microsoft.Playwright;
using System.Net.Http.Json;

namespace ShopPulse.E2E;

/// <summary>
/// Test E2E du back-office (phase 5) : connexion avec le compte stock-manager (demo),
/// passage d'une commande « en attente » à « payée », et modification du prix d'un
/// produit via le formulaire d'administration.
/// Exige : API (5299) + Web (5026) + Keycloak (8080) + db + Mailpit.
/// Compte du realm de dev : demo / demo1234 (rôle stock-manager).
/// </summary>
public class AdminE2eTests : IAsyncLifetime
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
       public async Task Admin_changes_order_status_and_edits_product()
       {
              // Une commande « en attente » garantie (créée en invité via l'API).
              var order = await _http.PostAsJsonAsync($"{ApiUrl}/api/orders", new
              {
                     email = "backoffice@example.fr",
                     fullName = "Test Back-office",
                     address = "8 rue de l'Admin",
                     postalCode = "69003",
                     lines = new[] { new { productId = 10, quantity = 1 } },
              });
              order.EnsureSuccessStatusCode();

              var page = await _browser.NewPageAsync();

              // Connexion stock-manager.
              await page.GotoAsync($"{WebUrl}/fr/admin/orders");
              await page.WaitForSelectorAsync("#username",
                     new PageWaitForSelectorOptions { Timeout = 15000 });
              await page.FillAsync("#username", "demo");
              await page.FillAsync("#password", "demo1234");
              await page.ClickAsync("#kc-login");
              await page.WaitForSelectorAsync("h1:has-text('Commandes')",
                     new PageWaitForSelectorOptions { Timeout = 15000 });

              // La commande passée par les autres tests est « en attente » : on la paie.
              await page.WaitForSelectorAsync("button:has-text('Marquer payée')");
              await page.ClickAsync("button:has-text('Marquer payée') >> nth=0");
              // Transition visible : la commande payée propose désormais « Marquer expédiée ».
              await page.WaitForSelectorAsync("button:has-text('Marquer expédiée')",
                     new PageWaitForSelectorOptions { Timeout = 10000 });

              // Édition produit : prix modifié puis relu dans la liste.
              await page.ClickAsync("a[href='/admin/products'] >> nth=0");
              await page.WaitForSelectorAsync("h1:has-text('Produits')");
              await page.ClickAsync("table tbody tr:first-child a:has-text('Modifier')");
              await page.WaitForSelectorAsync("#price");
              await page.FillAsync("#price", "123.45");
              await page.ClickAsync("button[type='submit']");
              await page.WaitForSelectorAsync("h1:has-text('Produits')",
                     new PageWaitForSelectorOptions { Timeout = 10000 });
              Assert.Contains("123,45", await page.TextContentAsync("table tbody tr:first-child"));
       }

       public async Task DisposeAsync()
       {
              _http.Dispose();
              await _browser.CloseAsync();
              _pw.Dispose();
       }
}
