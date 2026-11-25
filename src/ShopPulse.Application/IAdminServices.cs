using ShopPulse.Domain;

namespace ShopPulse.Application;

/// <summary>Gestion des produits (back-office). Tous les champs sont éditables ;
/// la catégorie est désignée par son nom (doit exister).</summary>
public sealed record ProductUpsert(
       string Sku,
       string Name,
       string Category,
       decimal Price,
       decimal? OldPrice,
       int Stock,
       string ImageUrl,
       string Description,
       IReadOnlyList<string> Features);

/// <summary>Résultat d'une écriture produit : le produit, ou un message de validation.</summary>
public sealed record ProductWriteResult(Product? Product, string? Error)
{
       public static ProductWriteResult Ok(Product p) => new(p, null);
       public static ProductWriteResult Invalid(string error) => new(null, error);
}

/// <summary>Port d'administration du catalogue (back-office, rôle stock-manager).</summary>
public interface IProductAdmin
{
       Task<ProductWriteResult> CreateAsync(ProductUpsert upsert, CancellationToken ct = default);
       Task<ProductWriteResult> UpdateAsync(int id, ProductUpsert upsert, CancellationToken ct = default);
}

/// <summary>Port d'administration des commandes (back-office, rôle stock-manager).</summary>
public interface IOrderAdmin
{
       /// <summary>Commandes paginées, plus récentes d'abord, filtrables par statut.</summary>
       Task<PagedResult<Order>> GetOrdersAsync(OrderStatus? status = null,
              int skip = 0, int take = 25, CancellationToken ct = default);

       /// <summary>Transition de statut validée (Pending→Paid|Cancelled, Paid→Shipped|Cancelled).
       /// Résultat nul si la commande n'existe pas ; erreur si la transition est interdite.</summary>
       Task<OrderWriteResult> SetStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct = default);
}

public sealed record OrderWriteResult(
       Order? Order,
       string? Error,
       IReadOnlyList<(int ProductId, int NewStock)> StockUpdates)
{
       public static OrderWriteResult Ok(Order o) => new(o, null, []);
       public static OrderWriteResult Ok(Order o, IReadOnlyList<(int, int)> updates) => new(o, null, updates);
       public static OrderWriteResult Invalid(string error) => new(null, error, []);
       public static OrderWriteResult NotFound() => new(null, null, []);
}
