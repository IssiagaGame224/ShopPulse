using Microsoft.AspNetCore.SignalR;

namespace ShopPulse.Api.Hubs;

/// <summary>Hub temps réel du stock : chaque client s'abonne aux groupes des produits affichés.</summary>
public sealed class StockHub : Hub
{
       public Task Subscribe(int productId) =>
              Groups.AddToGroupAsync(Context.ConnectionId, $"product-{productId}");

       public Task Unsubscribe(int productId) =>
              Groups.RemoveFromGroupAsync(Context.ConnectionId, $"product-{productId}");
}
