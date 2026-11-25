using ShopPulse.Domain;

namespace ShopPulse.Application;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total);

public interface IProductService
{
       Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default);
       Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
       Task<PagedResult<Product>> GetPageAsync(
              int skip, int take, string? search, string? category = null, string? sort = null,
              CancellationToken ct = default);
       Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);
}

/// <summary>Tris du catalogue (valeurs de l'URL ?sort=) : appliqués en SQL côté serveur.</summary>
public static class ProductSort
{
       public const string Default = "default";         // ordre du catalogue (id)
       public const string PriceAsc = "price-asc";
       public const string PriceDesc = "price-desc";
       public const string Newest = "newest";           // ids identity décroissants
       public const string Promo = "promo";             // promos d'abord

       public static bool IsValid(string? sort) =>
              sort is null or Default or PriceAsc or PriceDesc or Newest or Promo;
}
