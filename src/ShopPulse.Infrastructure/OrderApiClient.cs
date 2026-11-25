using ShopPulse.Application;
using ShopPulse.Contracts;
using ShopPulse.Domain;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShopPulse.Infrastructure;

/// <summary>Ports de commande côté Web : création et historique passent par l'API.
/// La clé d'idempotence voyage dans l'en-tête Idempotency-Key : un retry réseau ou un
/// double-clic rejoue la même commande au lieu d'en créer une seconde.
/// Le jeton d'accès de l'utilisateur est ajouté par AccessTokenHandler (client « api »).</summary>
public sealed class OrderApiClient(IHttpClientFactory factory) : IOrderPlacer, IOrderHistory, IPaymentClient
{
       private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
       {
              Converters = { new JsonStringEnumConverter() }   // statut en clair ("Pending"...)
       };

       public async Task<OrderPlacement> PlaceOrderAsync(PlaceOrderCommand cmd, CancellationToken ct = default)
       {
              var http = factory.CreateClient("api");
              using var request = new HttpRequestMessage(HttpMethod.Post, "api/orders")
              {
                     Content = JsonContent.Create(new PlaceOrderRequest(cmd.Email, cmd.FullName,
                            cmd.Address, cmd.PostalCode,
                            [.. cmd.Lines.Select(l => new PlaceOrderLineRequest(l.ProductId, l.Quantity))]))
              };
              request.Headers.Add("Idempotency-Key", cmd.IdempotencyKey);

              var response = await http.SendAsync(request, ct);
              return response.StatusCode switch
              {
                     HttpStatusCode.Created => OrderPlacement.Created(
                            (await response.Content.ReadFromJsonAsync<Order>(_json, ct))!, []),
                     HttpStatusCode.OK => OrderPlacement.Replayed(
                            (await response.Content.ReadFromJsonAsync<Order>(_json, ct))!),
                     HttpStatusCode.Conflict => OrderPlacement.StockConflict(await ReadErrorAsync(response, ct)),
                     HttpStatusCode.BadRequest => OrderPlacement.Invalid(await ReadErrorAsync(response, ct)),
                     _ => OrderPlacement.Failed($"Erreur inattendue de l'API ({(int)response.StatusCode})."),
              };
       }

       private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
       {
              try
              {
                     var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(_json, ct);
                     return payload is not null && payload.TryGetValue("error", out var error)
                            ? error
                            : "La commande a été refusée.";
              }
              catch (JsonException)
              {
                     return "La commande a été refusée.";
              }
       }

       public async Task<IReadOnlyList<Order>> GetMyOrdersAsync(CancellationToken ct = default)
              => await factory.CreateClient("api")
                     .GetFromJsonAsync<List<Order>>("api/orders/me", _json, ct) ?? [];

       public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api").GetAsync($"api/orders/{id}", ct);
              return response.StatusCode == HttpStatusCode.NotFound
                     ? null
                     : await response.Content.ReadFromJsonAsync<Order>(_json, ct);
       }

       public async Task<PaymentSession?> CreateSessionAsync(Guid orderId, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api").PostAsync($"api/orders/{orderId}/payment", null, ct);
              if (!response.IsSuccessStatusCode) return null;
              var dto = await response.Content.ReadFromJsonAsync<PaymentSessionDto>(_json, ct);
              return dto is null ? null
                     : new PaymentSession(dto.PaymentIntentId, dto.ClientSecret, dto.PublishableKey, dto.Live);
       }

       public async Task<bool> FakeConfirmAsync(Guid orderId, CancellationToken ct = default)
       {
              var response = await factory.CreateClient("api")
                     .PostAsync($"api/orders/{orderId}/payment/fake-confirm", null, ct);
              return response.IsSuccessStatusCode;
       }
}
