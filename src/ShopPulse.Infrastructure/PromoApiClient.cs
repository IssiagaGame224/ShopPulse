using ShopPulse.Application;
using ShopPulse.Contracts;
using ShopPulse.Domain;
using System.Net;
using System.Net.Http.Json;

namespace ShopPulse.Infrastructure;

/// <summary>Port des codes promo côté Web, via l'API (client « api » : le jeton
/// d'administration est propagé pour les opérations de back-office).</summary>
public sealed class PromoApiClient(IHttpClientFactory factory) : IPromoService
{
       private static readonly System.Text.Json.JsonSerializerOptions _json = new(System.Text.Json.JsonSerializerDefaults.Web)
       {
              Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }   // Kind en clair
       };
       public async Task<PromoPreview?> ValidateAsync(string code, decimal subtotal, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api").PostAsJsonAsync(
                     "api/promo-codes/validate", new ValidatePromoRequest(code, subtotal), ct);
              if (!response.IsSuccessStatusCode) return null;
              var dto = await response.Content.ReadFromJsonAsync<PromoValidationDto>(ct);
              return dto is null || !Enum.TryParse<PromoKind>(dto.Kind, out var kind)
                     ? null
                     : new PromoPreview(dto.Code, kind, dto.Value);
       }

       public async Task<IReadOnlyList<PromoCode>> GetAllAsync(CancellationToken ct = default) =>
              await factory.CreateClient("api")
                     .GetFromJsonAsync<List<PromoCode>>("api/admin/promo-codes", _json, ct) ?? [];

       public async Task<(PromoCode? Promo, string? Error)> CreateAsync(
              string code, PromoKind kind, decimal value, DateTimeOffset? expiresAt, int? maxUses,
              CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api").PostAsJsonAsync("api/admin/promo-codes",
                     new CreatePromoCodeRequest(code, kind.ToString(), value, expiresAt, maxUses), ct);
              if (response.IsSuccessStatusCode)
                     return (await response.Content.ReadFromJsonAsync<PromoCode>(_json, ct), null);
              return (null, await ReadErrorAsync(response, ct));
       }

       public async Task<bool> SetActiveAsync(int id, bool active, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api").PutAsJsonAsync(
                     $"api/admin/promo-codes/{id}/active", new SetPromoActiveRequest(active), ct);
              return response.IsSuccessStatusCode;
       }

       private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
       {
              try
              {
                     var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
                     return payload is not null && payload.TryGetValue("error", out var error)
                            ? error
                            : "L'opération a été refusée.";
              }
              catch (System.Text.Json.JsonException)
              {
                     return "L'opération a été refusée.";
              }
       }
}
