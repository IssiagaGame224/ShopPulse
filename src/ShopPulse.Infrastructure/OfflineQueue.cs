using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ShopPulse.Infrastructure;

public sealed record QueuedAction(Guid Id, string Type, string PayloadJson, DateTime CreatedAt, int Attempts = 0);

/// <summary>
/// Écritures faites hors ligne : mises en file (IndexedDB), puis rejouées DANS L'ORDRE.
/// Chaque entrée porte une clé d'idempotence : un rejeu ne duplique rien.
/// </summary>
public sealed class OfflineQueue(BrowserDb db, IHttpClientFactory http)
{
       public async Task EnqueueAsync(string type, object payload)
       {
              var item = new QueuedAction(Guid.NewGuid(), type, JsonSerializer.Serialize(payload), DateTime.UtcNow);
              await db.PutAsync("queue", item);
       }

       public async Task FlushAsync(CancellationToken ct = default)
       {
              var items = (await db.GetAllAsync<QueuedAction>("queue")).OrderBy(i => i.CreatedAt);
              var client = http.CreateClient("api");

              foreach (var item in items)
              {
                     using var req = new HttpRequestMessage(HttpMethod.Post, $"api/actions/{item.Type}")
                     {
                            Content = new StringContent(item.PayloadJson, Encoding.UTF8, "application/json")
                     };
                     req.Headers.Add("Idempotency-Key", item.Id.ToString()); // un rejeu ne duplique rien

                     var res = await client.SendAsync(req, ct);

                     if (res.IsSuccessStatusCode || (int)res.StatusCode is >= 400 and < 500)
                            await db.RemoveAsync("queue", item.Id.ToString());  // fait, ou définitivement refusé
                     else
                            break;                                              // 5xx : on réessaiera plus tard
              }
       }
}
