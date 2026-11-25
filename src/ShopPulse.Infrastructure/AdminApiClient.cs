using ShopPulse.Application;
using ShopPulse.Contracts;
using ShopPulse.Domain;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShopPulse.Infrastructure;

/// <summary>Ports d'administration côté Web (back-office) : le jeton de l'utilisateur
/// (rôle stock-manager) est propagé par AccessTokenHandler sur le client « api ».</summary>
public sealed class AdminApiClient(IHttpClientFactory factory) : IOrderAdmin, IProductAdmin
{
       private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
       {
              Converters = { new JsonStringEnumConverter() }
       };

       public async Task<PagedResult<Order>> GetOrdersAsync(OrderStatus? status = null,
              int skip = 0, int take = 25, CancellationToken ct = default)
       {
              var url = $"api/admin/orders?page={skip / Math.Max(1, take) + 1}";
              if (status is { } s) url += $"&status={s}";
              return await factory.CreateClient("api")
                     .GetFromJsonAsync<PagedResult<Order>>(url, _json, ct)
                     ?? new PagedResult<Order>([], 0);
       }

       public async Task<OrderWriteResult> SetStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api").PutAsJsonAsync(
                     $"api/admin/orders/{orderId}/status", new SetOrderStatusRequest(status.ToString()), ct);
              return response.StatusCode switch
              {
                     HttpStatusCode.OK => OrderWriteResult.Ok(
                            (await response.Content.ReadFromJsonAsync<Order>(_json, ct))!),
                     HttpStatusCode.NotFound => OrderWriteResult.NotFound(),
                     HttpStatusCode.Conflict => OrderWriteResult.Invalid(await ReadErrorAsync(response, ct)),
                     _ => OrderWriteResult.Invalid($"Erreur inattendue de l'API ({(int)response.StatusCode})."),
              };
       }

       public async Task<ProductWriteResult> CreateAsync(ProductUpsert upsert, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api").PostAsJsonAsync("api/admin/products", upsert, ct);
              return await ToResultAsync(response, ct);
       }

       public async Task<ProductWriteResult> UpdateAsync(int id, ProductUpsert upsert, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api").PutAsJsonAsync($"api/admin/products/{id}", upsert, ct);
              return await ToResultAsync(response, ct);
       }

       private static async Task<ProductWriteResult> ToResultAsync(HttpResponseMessage response, CancellationToken ct)
       {
              if (response.IsSuccessStatusCode)
                     return ProductWriteResult.Ok(
                            (await response.Content.ReadFromJsonAsync<Product>(_json, ct))!);
              return ProductWriteResult.Invalid(await ReadErrorAsync(response, ct));
       }

       private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
       {
              try
              {
                     var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(_json, ct);
                     return payload is not null && payload.TryGetValue("error", out var error)
                            ? error
                            : "L'opération a été refusée.";
              }
              catch (JsonException)
              {
                     return "L'opération a été refusée.";
              }
       }
}
