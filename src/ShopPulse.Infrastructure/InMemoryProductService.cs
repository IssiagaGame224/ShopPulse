using ShopPulse.Application;
using ShopPulse.Domain;
using ShopPulse.Infrastructure.Persistence;

namespace ShopPulse.Infrastructure;

public class InMemoryProductService : IProductService
{
       // Le catalogue vient de CatalogSeed : source unique partagée avec le seeding EF Core.
       private static readonly List<Product> _data = [.. CatalogSeed.BuildProducts()];

       public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
       {
              await Task.Delay(1500, ct);  // latence simulée (outil pédagogique : streaming, cache)
              return _data;
       }

       public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
       {
              await Task.Delay(300, ct);
              return _data.FirstOrDefault(p => p.Id == id);
       }

       public async Task<PagedResult<Product>> GetPageAsync(
              int skip, int take, string? search, string? category = null, string? sort = null,
              CancellationToken ct = default)
       {
              await Task.Delay(150, ct); // latence réaliste
              var query = _data.AsEnumerable();
              if (!string.IsNullOrWhiteSpace(category))
                     query = query.Where(p => p.Category == category);
              if (!string.IsNullOrWhiteSpace(search))
                     query = query.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

              query = sort switch
              {
                     ProductSort.PriceAsc => query.OrderBy(p => p.Price),
                     ProductSort.PriceDesc => query.OrderByDescending(p => p.Price),
                     ProductSort.Newest => query.OrderByDescending(p => p.Id),
                     ProductSort.Promo => query.OrderByDescending(p => p.IsOnSale),
                     _ => query,
              };

              var list = query.ToList();
              return new PagedResult<Product>(list.Skip(skip).Take(take).ToList(), list.Count);
       }

       public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default) =>
              Task.FromResult<IReadOnlyList<string>>([.. CatalogSeed.Catégories.Select(c => c.Name)]);

       /// <summary>Modification de stock côté API (back-office / exercice temps réel).
       /// L'écriture n'est PAS thread-safe : acceptable pour un service en mémoire de dev,
       /// la vraie implémentation passerait par la base (module 5, concept Conflits).</summary>
       public void SetStock(int id, int stock)
       {
              var i = _data.FindIndex(p => p.Id == id);
              if (i >= 0) _data[i] = _data[i] with { Stock = stock };
       }
}
