namespace ShopPulse.Application;

/// <summary>Port du flux temps réel de stock (implémentation : hub SignalR côté Infrastructure).</summary>
public interface IStockFeed : IAsyncDisposable
{
       event Action<int, int>? StockChanged;
       Task StartAsync();
       Task SubscribeAsync(int productId);
       Task UnsubscribeAsync(int productId);
}
