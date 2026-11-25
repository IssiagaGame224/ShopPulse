namespace ShopPulse.Infrastructure;

/// <summary>
/// SEUL Singleton : aucun état utilisateur, juste un bus de notifications.
/// Le module 6 remplacera ce mécanisme par un hub SignalR.
/// </summary>
public sealed class CatalogNotifier
{
       public event Action<int>? ProductChanged;        // id du produit

       public void Publish(int productId) => ProductChanged?.Invoke(productId);
}
