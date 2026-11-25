using Microsoft.AspNetCore.SignalR;
using ShopPulse.Api.Hubs;

namespace ShopPulse.Api;

public interface IStockPublisher
{
       Task PublishAsync(int productId, int stock);
}

public sealed class StockPublisher(IHubContext<StockHub> hub) : IStockPublisher
{
       public Task PublishAsync(int productId, int stock) =>
              hub.Clients.Group($"product-{productId}").SendAsync("StockChanged", productId, stock);
}
