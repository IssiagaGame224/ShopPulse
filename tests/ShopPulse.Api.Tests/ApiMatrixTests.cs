using ShopPulse.Domain;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShopPulse.Api.Tests;

/// <summary>La matrice de l'API en tests automatisés : accès (401/403/404/409),
/// totaux recalculés serveur, idempotence, restock à l'annulation.
/// Jetons réels via Keycloak de dev (grant direct) — voir ApiFactory.</summary>
[Collection("api")]
public class ApiMatrixTests(ApiFactory factory)
{
       private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
       {
              Converters = { new JsonStringEnumConverter() }
       };

       private HttpClient Client() => factory.CreateClient();

       private static async Task<string> TokenAsync(string user, string password)
       {
              // HttpClient RÉEL : le client de la factory route tout en mémoire vers l'app
              // de test — Keycloak doit être joint par le réseau.
              using var http = new HttpClient();
              var response = await http.PostAsync(
                     "http://localhost:8080/realms/shoppulse/protocol/openid-connect/token",
                     new FormUrlEncodedContent(new Dictionary<string, string>
                     {
                            ["grant_type"] = "password",
                            ["client_id"] = "shoppulse-web",
                            ["client_secret"] = "dev-secret-shoppulse",
                            ["username"] = user,
                            ["password"] = password,
                            ["scope"] = "openid",
                     }));
              response.EnsureSuccessStatusCode();
              var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
              return payload.GetProperty("access_token").GetString()!;
       }

       private static async Task<JsonElement> PlaceOrderAsync(HttpClient http, string key,
              string email, params (int ProductId, int Quantity)[] lines)
       {
              using var request = new HttpRequestMessage(HttpMethod.Post, "api/orders")
              {
                     Content = JsonContent.Create(new
                     {
                            email,
                            fullName = "Test Intégration",
                            address = "1 rue des Tests",
                            postalCode = "75001",
                            lines = lines.Select(l => new { productId = l.ProductId, quantity = l.Quantity }),
                     })
              };
              request.Headers.Add("Idempotency-Key", key);
              var response = await http.SendAsync(request);
              response.EnsureSuccessStatusCode();
              return await response.Content.ReadFromJsonAsync<JsonElement>();
       }

       private static async Task<int> StockAsync(HttpClient http, int productId)
       {
              var product = await http.GetFromJsonAsync<JsonElement>($"api/products/{productId}");
              return product.GetProperty("stock").GetInt32();
       }

       // ---- Santé & lecture publique ----

       [Fact]
       public async Task Health_IsHealthy_WithAllDependencies()
       {
              var health = await Client().GetFromJsonAsync<JsonElement>("health");
              Assert.Equal("Healthy", health.GetProperty("status").GetString());
       }

       [Fact]
       public async Task GetProducts_ReturnsSeededCatalog()
       {
              var products = await Client().GetFromJsonAsync<List<JsonElement>>("api/products");
              Assert.True(products!.Count >= 200);
       }

       [Fact]
       public async Task GetProduct_UnknownId_Returns404() =>
              Assert.Equal(HttpStatusCode.NotFound,
                     (await Client().GetAsync("api/products/99999")).StatusCode);

       // ---- Écritures de stock : la matrice d'autorisation ----

       [Fact]
       public async Task SetStock_Anonymous_Returns401()
       {
              var response = await Client().PostAsJsonAsync("api/stock", new { productId = 1, stock = 10 });
              Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
       }

       [Fact]
       public async Task SetStock_WithoutRole_Returns403()
       {
              var http = Client();
              http.DefaultRequestHeaders.Authorization =
                     new AuthenticationHeaderValue("Bearer", await TokenAsync("client", "client1234"));
              var response = await http.PostAsJsonAsync("api/stock", new { productId = 1, stock = 10 });
              Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
       }

       [Fact]
       public async Task SetStock_StockManager_Returns204_AndPersists()
       {
              var http = Client();
              http.DefaultRequestHeaders.Authorization =
                     new AuthenticationHeaderValue("Bearer", await TokenAsync("demo", "demo1234"));
              var response = await http.PostAsJsonAsync("api/stock", new { productId = 10, stock = 42 });
              Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
              Assert.Equal(42, await StockAsync(Client(), 10));
       }

       // ---- Commandes : totaux serveur, idempotence ----

       [Fact]
       public async Task PlaceOrder_SmallOrder_HasShippingCost()
       {
              var order = await PlaceOrderAsync(Client(), $"it-ship-{Guid.NewGuid():N}", "a@b.fr", (2, 1));
              Assert.Equal(5.90m, order.GetProperty("shippingCost").GetDecimal());
              Assert.Equal(13.00m + 5.90m, order.GetProperty("total").GetDecimal());
       }

       [Fact]
       public async Task PlaceOrder_AboveThreshold_ShippingIsFree()
       {
              var order = await PlaceOrderAsync(Client(), $"it-free-{Guid.NewGuid():N}", "a@b.fr", (2, 6));
              Assert.Equal(0m, order.GetProperty("shippingCost").GetDecimal());
              Assert.Equal(78.00m, order.GetProperty("total").GetDecimal());
       }

       [Fact]
       public async Task PlaceOrder_SameIdempotencyKey_ReplaysWithoutDoubleDecrement()
       {
              var http = Client();
              var key = $"it-idem-{Guid.NewGuid():N}";
              var stockBefore = await StockAsync(http, 11);

              var first = await PlaceOrderAsync(http, key, "a@b.fr", (11, 1));
              var second = await PlaceOrderAsync(http, key, "a@b.fr", (11, 1));

              Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
              Assert.Equal(stockBefore - 1, await StockAsync(http, 11));
       }

       // ---- Contrôle d'accès sur les commandes ----

       [Fact]
       public async Task GetOrder_LinkedOrder_IsHiddenFromAnonymous_AndOtherUsers()
       {
              var owner = Client();
              owner.DefaultRequestHeaders.Authorization =
                     new AuthenticationHeaderValue("Bearer", await TokenAsync("client", "client1234"));
              var order = await PlaceOrderAsync(owner, $"it-own-{Guid.NewGuid():N}", "client@shoppulse.local", (12, 1));
              var id = order.GetProperty("id").GetGuid();

              Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync($"api/orders/{id}")).StatusCode);
              Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"api/orders/{id}")).StatusCode);

              var other = Client();
              other.DefaultRequestHeaders.Authorization =
                     new AuthenticationHeaderValue("Bearer", await TokenAsync("demo", "demo1234"));
              Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"api/orders/{id}")).StatusCode);
       }

       [Fact]
       public async Task GetMyOrders_RequiresToken()
       {
              Assert.Equal(HttpStatusCode.Unauthorized,
                     (await Client().GetAsync("api/orders/me")).StatusCode);

              var http = Client();
              http.DefaultRequestHeaders.Authorization =
                     new AuthenticationHeaderValue("Bearer", await TokenAsync("client", "client1234"));
              await PlaceOrderAsync(http, $"it-me-{Guid.NewGuid():N}", "client@shoppulse.local", (15, 1));

              var orders = await http.GetFromJsonAsync<List<Order>>("api/orders/me", _json);
              Assert.NotEmpty(orders!);
       }

       // ---- Tri catalogue & suivi invité signé ----

       [Fact]
       public async Task GetProductsPage_PriceAscending_IsSorted()
       {
              var page = await Client().GetFromJsonAsync<JsonElement>(
                     "api/products/page?take=50&sort=price-asc");
              var prices = page.GetProperty("items").EnumerateArray()
                     .Select(p => p.GetProperty("price").GetDecimal()).ToList();
              Assert.Equal(prices.OrderBy(p => p).ToList(), prices);
       }

       [Fact]
       public async Task GetProductsPage_UnknownSort_Returns400() =>
              Assert.Equal(HttpStatusCode.BadRequest,
                     (await Client().GetAsync("api/products/page?sort=nimporte")).StatusCode);

       [Fact]
       public async Task TrackOrder_ValidSignature_ReturnsOrder_InvalidIs404()
       {
              var order = await PlaceOrderAsync(Client(), $"it-track-{Guid.NewGuid():N}", "a@b.fr", (16, 1));
              var id = order.GetProperty("id").GetGuid();
              var sig = ShopPulse.Infrastructure.OrderTracking.ComputeSignature(
                     id, ShopPulse.Infrastructure.OrderTracking.DefaultDevKey);

              var tracked = await Client().GetFromJsonAsync<JsonElement>($"api/orders/{id}/track?sig={sig}");
              Assert.Equal(id, tracked.GetProperty("id").GetGuid());

              Assert.Equal(HttpStatusCode.NotFound,
                     (await Client().GetAsync($"api/orders/{id}/track?sig=0000000000000000")).StatusCode);
       }

       // ---- Back-office : matrice + transitions + remboursement/restock ----

       [Fact]
       public async Task AdminOrders_Anonymous401_DemoOk()
       {
              Assert.Equal(HttpStatusCode.Unauthorized,
                     (await Client().GetAsync("api/admin/orders")).StatusCode);

              var http = Client();
              http.DefaultRequestHeaders.Authorization =
                     new AuthenticationHeaderValue("Bearer", await TokenAsync("demo", "demo1234"));
              Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("api/admin/orders")).StatusCode);
       }

       [Fact]
       public async Task CancelPaidOrder_RefundsAndRestocks_ThenTransitionsAreClosed()
       {
              var http = Client();
              var stockBefore = await StockAsync(http, 13);
              var order = await PlaceOrderAsync(http, $"it-cancel-{Guid.NewGuid():N}", "a@b.fr", (13, 1));
              var id = order.GetProperty("id").GetGuid();

              // Paiement simulé (session d'abord : l'intent est attaché à la commande).
              (await http.PostAsync($"api/orders/{id}/payment", null)).EnsureSuccessStatusCode();
              (await http.PostAsync($"api/orders/{id}/payment/fake-confirm", null)).EnsureSuccessStatusCode();
              Assert.Equal(stockBefore - 1, await StockAsync(http, 13));

              var admin = Client();
              admin.DefaultRequestHeaders.Authorization =
                     new AuthenticationHeaderValue("Bearer", await TokenAsync("demo", "demo1234"));

              var cancelled = await admin.PutAsJsonAsync($"api/admin/orders/{id}/status", new { status = "Cancelled" });
              Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
              Assert.Equal(stockBefore, await StockAsync(http, 13));   // restock

              // Terminale : une commande annulée ne peut plus être expédiée.
              var invalid = await admin.PutAsJsonAsync($"api/admin/orders/{id}/status", new { status = "Shipped" });
              Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
       }
}
