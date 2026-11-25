using ShopPulse.Domain;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ShopPulse.Api.Tests;

/// <summary>Codes promo (docs/29) : validation sans consommation, application à la
/// commande (remise recalculée serveur, livraison sur le sous-total remisé, compteur
/// consommé dans la transaction), code invalide → 400, administration réservée.</summary>
[Collection("api")]
public class PromoTests(ApiFactory factory)
{
       private HttpClient Client() => factory.CreateClient();

       private static async Task<string> TokenAsync(string user, string password)
       {
              using var http = new HttpClient();   // vrai réseau pour Keycloak (voir ApiFactory)
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

       private static HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
       {
              var request = new HttpRequestMessage(method, url);
              request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
              if (body is not null) request.Content = JsonContent.Create(body);
              return request;
       }

       private static async Task<int> InStockProductAsync(HttpClient http)
       {
              var catalog = await http.GetFromJsonAsync<List<JsonElement>>("api/products?page=1&pageSize=100");
              return catalog!.First(p => p.GetProperty("stock").GetInt32() > 1
                     && p.GetProperty("price").GetDecimal() >= 10).GetProperty("id").GetInt32();
       }

       [Fact]
       public async Task Promo_validation_order_application_and_administration()
       {
              var http = Client();

              // 1. Les codes semés existent ; la validation calcule la remise SANS consommer.
              var validation = await http.PostAsJsonAsync("api/promo-codes/validate",
                     new { code = "bienvenue10", subtotal = 80m });   // casse libre
              Assert.Equal(HttpStatusCode.OK, validation.StatusCode);
              var dto = await validation.Content.ReadFromJsonAsync<JsonElement>();
              Assert.Equal("BIENVENUE10", dto.GetProperty("code").GetString());
              Assert.Equal(8.00m, dto.GetProperty("discount").GetDecimal());   // 10 % de 80

              // Montant fixe plafonné au sous-total.
              validation = await http.PostAsJsonAsync("api/promo-codes/validate",
                     new { code = "FIXE5", subtotal = 3m });
              dto = await validation.Content.ReadFromJsonAsync<JsonElement>();
              Assert.Equal(3.00m, dto.GetProperty("discount").GetDecimal());

              // Code inconnu → 400.
              validation = await http.PostAsJsonAsync("api/promo-codes/validate",
                     new { code = "NOPE", subtotal = 50m });
              Assert.Equal(HttpStatusCode.BadRequest, validation.StatusCode);

              // 2. Commande avec code : remise recalculée serveur, total cohérent.
              var productId = await InStockProductAsync(http);
              var product = await http.GetFromJsonAsync<JsonElement>($"api/products/{productId}");
              var price = product.GetProperty("price").GetDecimal();

              var orderResponse = await http.PostAsJsonAsync("api/orders", new
              {
                     email = "promo@example.fr",
                     fullName = "Test Promo",
                     address = "2 rue des Codes",
                     postalCode = "69001",
                     lines = new[] { new { productId, quantity = 1 } },
                     promoCode = "BIENVENUE10",
              });
              Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
              var order = await orderResponse.Content.ReadFromJsonAsync<Order>(
                     new JsonSerializerOptions(JsonSerializerDefaults.Web)
                     { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
              var expectedDiscount = Math.Round(price * 0.10m, 2);
              Assert.Equal("BIENVENUE10", order!.PromoCode);
              Assert.Equal(expectedDiscount, order.Discount);
              Assert.Equal(price - expectedDiscount + order.ShippingCost, order.Total);

              // 3. La consommation a incrémenté le compteur (visible en admin).
              var adminToken = await TokenAsync("demo", "demo1234");
              var listResponse = await http.SendAsync(Authed(HttpMethod.Get, "api/admin/promo-codes", adminToken));
              listResponse.EnsureSuccessStatusCode();
              var promos = await listResponse.Content.ReadFromJsonAsync<List<PromoCode>>(
                     new JsonSerializerOptions(JsonSerializerDefaults.Web)
                     { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
              Assert.True(promos!.Single(p => p.Code == "BIENVENUE10").UsedCount >= 1);

              // 4. Code invalide à la commande → 400 (jamais de remise fantôme).
              orderResponse = await http.PostAsJsonAsync("api/orders", new
              {
                     email = "promo@example.fr",
                     fullName = "Test Promo",
                     address = "2 rue des Codes",
                     postalCode = "69001",
                     lines = new[] { new { productId, quantity = 1 } },
                     promoCode = "EXPIRE",
              });
              Assert.Equal(HttpStatusCode.BadRequest, orderResponse.StatusCode);

              // 5. Administration : création (code normalisé), doublon refusé,
              // désactivation → le code ne valide plus ; un client simple n'a pas le droit.
              var create = await http.SendAsync(Authed(HttpMethod.Post, "api/admin/promo-codes", adminToken,
                     new { code = "test-e2e", kind = "Percent", value = 15m, expiresAt = (DateTimeOffset?)null, maxUses = (int?)5 }));
              Assert.Equal(HttpStatusCode.OK, create.StatusCode);
              var created = await create.Content.ReadFromJsonAsync<PromoCode>(
                     new JsonSerializerOptions(JsonSerializerDefaults.Web)
                     { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
              Assert.Equal("TEST-E2E", created!.Code);

              create = await http.SendAsync(Authed(HttpMethod.Post, "api/admin/promo-codes", adminToken,
                     new { code = "TEST-E2E", kind = "Percent", value = 5m, expiresAt = (DateTimeOffset?)null, maxUses = (int?)null }));
              Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

              var toggle = await http.SendAsync(Authed(HttpMethod.Put,
                     $"api/admin/promo-codes/{created.Id}/active", adminToken, new { active = false }));
              Assert.Equal(HttpStatusCode.NoContent, toggle.StatusCode);
              validation = await http.PostAsJsonAsync("api/promo-codes/validate",
                     new { code = "TEST-E2E", subtotal = 50m });
              Assert.Equal(HttpStatusCode.BadRequest, validation.StatusCode);

              var clientToken = await TokenAsync("client", "client1234");
              var forbidden = await http.SendAsync(Authed(HttpMethod.Get, "api/admin/promo-codes", clientToken));
              Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
       }
}
