using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using ShopPulse.Application;

namespace ShopPulse.Infrastructure;

/// <summary>
/// Connexion au hub SignalR de l'API, une par circuit (Scoped).
/// Pour beaucoup d'utilisateurs, on partagerait plutôt une seule connexion Singleton
/// qui diffuserait en interne à tous les circuits.
/// </summary>
public sealed class StockFeed(IConfiguration cfg) : IStockFeed
{
       private HubConnection? _conn;
       public event Action<int, int>? StockChanged;

       public async Task StartAsync()
       {
              if (_conn is not null) return;
              _conn = new HubConnectionBuilder()
                      .WithUrl($"{cfg["Api:BaseUrl"]}hubs/stock")
                      .WithAutomaticReconnect()
                      .Build();
              _conn.On<int, int>("StockChanged", (id, stock) => StockChanged?.Invoke(id, stock));
              await _conn.StartAsync();
       }

       public Task SubscribeAsync(int productId) => _conn!.InvokeAsync("Subscribe", productId);

       public Task UnsubscribeAsync(int productId) => _conn!.InvokeAsync("Unsubscribe", productId);

       public async ValueTask DisposeAsync()
       {
              if (_conn is not null) await _conn.DisposeAsync();
       }
}
