using ShopPulse.Domain;
using ShopPulse.SharedKernel;

namespace ShopPulse.Features.Catalog;

public sealed class ProductStore : Store<EntityState<Product>>
{
       public ProductStore() : base(EntityState<Product>.Empty) { }

       public void Load(IEnumerable<Product> products) =>
              Set(s => s.UpsertMany(products, p => p.Id));

       /// <summary>Mise à jour d'un seul produit (ex. stock reçu en temps réel, module 6).</summary>
       public void UpdateStock(int id, int stock) => Set(s =>
              s.ById.TryGetValue(id, out var p)
                      ? s with { ById = s.ById.SetItem(id, p with { Stock = stock }) }
                      : s);
}
