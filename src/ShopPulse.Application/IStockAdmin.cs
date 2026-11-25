namespace ShopPulse.Application;

/// <summary>Port d'administration du stock (back-office autorisé).</summary>
public interface IStockAdmin
{
       Task SetStockAsync(int productId, int stock, CancellationToken ct = default);
}
