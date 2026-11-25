using Microsoft.Extensions.Caching.Hybrid;
using ShopPulse.Application;
using ShopPulse.Domain;

namespace ShopPulse.Infrastructure;

/// <summary>Décorateur de cache : le C# de la composition racine, sans toucher aux consommateurs.</summary>
public class CachedProductService(IProductService inner, HybridCache cache) : IProductService
{
       public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default) =>
              await cache.GetOrCreateAsync("products:all",
                     async token => await inner.GetAllAsync(token),
                     new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
                     tags: ["catalog"], cancellationToken: ct);

       public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
              => inner.GetByIdAsync(id, ct);

       public Task<PagedResult<Product>> GetPageAsync(int skip, int take, string? search, string? category = null, CancellationToken ct = default)
              => inner.GetPageAsync(skip, take, search, category, ct);

       public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default)
              => inner.GetCategoriesAsync(ct);
}
