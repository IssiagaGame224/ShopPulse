using ShopPulse.Application;
using ShopPulse.Contracts;
using ShopPulse.Domain;
using System.Net;
using System.Net.Http.Json;

namespace ShopPulse.Infrastructure;

/// <summary>Port des avis côté Web, via l'API. Le client « api » ajoute le jeton de
/// l'utilisateur quand il existe (AccessTokenHandler) : la lecture publique part
/// anonyme, la soumission et la modération portent l'identité.</summary>
public sealed class ReviewApiClient(IHttpClientFactory factory) : IReviewService
{
       public async Task<ProductReviews?> GetForProductAsync(int productId, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api")
                     .GetAsync($"api/products/{productId}/reviews", ct);
              return response.StatusCode == HttpStatusCode.NotFound
                     ? null
                     : await response.Content.ReadFromJsonAsync<ProductReviews>(ct);
       }

       public async Task<ReviewSubmitResult> SubmitAsync(
              int productId, int rating, string comment, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api").PostAsJsonAsync(
                     $"api/products/{productId}/reviews", new UpsertReviewRequest(rating, comment), ct);
              return response.StatusCode switch
              {
                     HttpStatusCode.OK => new ReviewSubmitResult.Ok(
                            (await response.Content.ReadFromJsonAsync<Review>(ct))!),
                     HttpStatusCode.Unauthorized => new ReviewSubmitResult.Unauthorized(),
                     HttpStatusCode.BadRequest => new ReviewSubmitResult.Invalid(
                            await ReadErrorAsync(response, ct)),
                     _ => new ReviewSubmitResult.Failed(
                            $"Erreur inattendue de l'API ({(int)response.StatusCode})."),
              };
       }

       public async Task<IReadOnlyList<AdminReview>> GetForModerationAsync(CancellationToken ct = default) =>
              await factory.CreateClient("api")
                     .GetFromJsonAsync<List<AdminReview>>("api/admin/reviews", ct) ?? [];

       public async Task<bool> DeleteAsync(int reviewId, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api")
                     .DeleteAsync($"api/admin/reviews/{reviewId}", ct);
              return response.IsSuccessStatusCode;
       }

       private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
       {
              try
              {
                     var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
                     return payload is not null && payload.TryGetValue("error", out var error)
                            ? error
                            : "L'avis a été refusé.";
              }
              catch (System.Text.Json.JsonException)
              {
                     return "L'avis a été refusé.";
              }
       }
}
