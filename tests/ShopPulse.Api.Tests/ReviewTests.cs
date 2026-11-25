using ShopPulse.Domain;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ShopPulse.Api.Tests;

/// <summary>Avis clients (docs/28) : soumission authentifiée (l'auteur vient du jeton),
/// upsert — un seul avis par (client, produit) —, validation, agrégat sur le produit,
/// modération réservée au rôle stock-manager.</summary>
[Collection("api")]
public class ReviewTests(ApiFactory factory)
{
       private const int ProductId = 5;

       private HttpClient Client() => factory.CreateClient();

       private static async Task<string> TokenAsync(string user, string password)
       {
              // HttpClient RÉEL : le client de la factory route tout en mémoire (voir ApiFactory).
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
              var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
              return payload.GetProperty("access_token").GetString()!;
       }

       private static HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
       {
              var request = new HttpRequestMessage(method, url);
              request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
              if (body is not null) request.Content = JsonContent.Create(body);
              return request;
       }

       [Fact]
       public async Task Review_flow_submit_upsert_aggregate_and_moderation()
       {
              var http = Client();
              var clientToken = await TokenAsync("client", "client1234");
              var adminToken = await TokenAsync("demo", "demo1234");

              // 1. Soumission authentifiée : l'auteur est le preferred_username du jeton.
              var response = await http.SendAsync(Authed(HttpMethod.Post,
                     $"api/products/{ProductId}/reviews", clientToken,
                     new { rating = 4, comment = "Très bon produit, test d'intégration." }));
              Assert.Equal(HttpStatusCode.OK, response.StatusCode);
              var review = await response.Content.ReadFromJsonAsync<Review>();
              Assert.Equal("client", review!.AuthorName);
              Assert.Equal(4, review.Rating);

              // 2. Lecture publique : l'avis est là, l'agrégat aussi.
              var page = await http.GetFromJsonAsync<ProductReviews>($"api/products/{ProductId}/reviews");
              Assert.NotNull(page);
              Assert.Contains(page!.Reviews, r => r.AuthorName == "client");
              Assert.True(page.Rating.Count >= 1);
              Assert.InRange(page.Rating.Average, 1, 5);

              // 3. Agrégat remonté sur le produit lui-même (catalogue).
              var product = await http.GetFromJsonAsync<Product>($"api/products/{ProductId}");
              Assert.Equal(page.Rating.Count, product!.ReviewCount);
              Assert.Equal(page.Rating.Average, product.AverageRating);

              // 4. Upsert : un second envoi REMPLACE l'avis — le compteur ne bouge pas.
              response = await http.SendAsync(Authed(HttpMethod.Post,
                     $"api/products/{ProductId}/reviews", clientToken,
                     new { rating = 5, comment = "Encore mieux à l'usage." }));
              response.EnsureSuccessStatusCode();
              var after = await http.GetFromJsonAsync<ProductReviews>($"api/products/{ProductId}/reviews");
              Assert.Equal(page.Rating.Count, after!.Rating.Count);
              Assert.Equal(5, after.Reviews.Single(r => r.AuthorName == "client").Rating);

              // 5. Validation : note hors bornes et commentaire vide refusés.
              response = await http.SendAsync(Authed(HttpMethod.Post,
                     $"api/products/{ProductId}/reviews", clientToken, new { rating = 6, comment = "x" }));
              Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
              response = await http.SendAsync(Authed(HttpMethod.Post,
                     $"api/products/{ProductId}/reviews", clientToken, new { rating = 3, comment = "  " }));
              Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

              // 6. Produit inconnu → 404 ; anonyme → 401.
              response = await http.SendAsync(Authed(HttpMethod.Post,
                     "api/products/999999/reviews", clientToken, new { rating = 3, comment = "Fantôme" }));
              Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
              response = await http.PostAsJsonAsync($"api/products/{ProductId}/reviews",
                     new { rating = 3, comment = "Anonyme" });
              Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

              // 7. Modération : le client n'a pas le droit (403), l'admin liste et supprime.
              var reviewId = after.Reviews.Single(r => r.AuthorName == "client").Id;
              response = await http.SendAsync(Authed(HttpMethod.Delete, $"api/admin/reviews/{reviewId}", clientToken));
              Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

              var moderation = await http.SendAsync(Authed(HttpMethod.Get, "api/admin/reviews", adminToken));
              moderation.EnsureSuccessStatusCode();
              var list = await moderation.Content.ReadFromJsonAsync<List<AdminReview>>();
              Assert.Contains(list!, r => r.Id == reviewId && r.ProductName.Length > 0);

              response = await http.SendAsync(Authed(HttpMethod.Delete, $"api/admin/reviews/{reviewId}", adminToken));
              Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
              var final = await http.GetFromJsonAsync<ProductReviews>($"api/products/{ProductId}/reviews");
              Assert.DoesNotContain(final!.Reviews, r => r.Id == reviewId);
       }
}
