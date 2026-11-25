using ShopPulse.Domain;
using ShopPulse.SharedKernel;

namespace ShopPulse.Features.Cart;

/// <summary>Sélecteurs dérivés du panier : fonctions pures, testables sans Blazor.</summary>
public static class CartSelectors
{
       public static decimal Total(this CartState cart, EntityState<Product> catalog) =>
              cart.Lines.Sum(l => catalog.ById.TryGetValue(l.ProductId, out var p) ? p.Price * l.Quantity : 0m);

       public static bool HasOutOfStock(this CartState cart, EntityState<Product> catalog) =>
              cart.Lines.Any(l => catalog.ById.TryGetValue(l.ProductId, out var p) && p.Stock < l.Quantity);
}
