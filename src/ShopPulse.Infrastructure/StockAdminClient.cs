using ShopPulse.Application;
using System.Net.Http.Json;

namespace ShopPulse.Infrastructure;

public sealed class StockAdminClient(IHttpClientFactory factory) : IStockAdmin
{
       public async Task SetStockAsync(int productId, int stock, CancellationToken ct = default)
       {
              var http = factory.CreateClient("api");
              var res = await http.PostAsJsonAsync("api/stock", new { productId, stock }, ct);
              res.EnsureSuccessStatusCode();
       }
}
