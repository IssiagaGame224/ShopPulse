using System.Collections.Immutable;
using ShopPulse.SharedKernel;

namespace ShopPulse.Features.Cart;

/// <summary>Panier du fil rouge. Implémente ICartGateway pour les autres features.</summary>
public sealed class CartStore : Store<CartState>, ICartGateway
{
       public CartStore() : base(CartState.Empty) { }

       public void Add(int productId, int quantity = 1) => Set(s =>
       {
              var i = s.Lines.FindIndex(l => l.ProductId == productId);
              return i < 0
                     ? s with { Lines = s.Lines.Add(new CartLine(productId, quantity)) }
                     : s with { Lines = s.Lines.SetItem(i, s.Lines[i] with { Quantity = s.Lines[i].Quantity + quantity }) };
       });

       public void SetQuantity(int productId, int quantity) => Set(s =>
              quantity <= 0
                     ? s with { Lines = s.Lines.RemoveAll(l => l.ProductId == productId) }
                     : s with
                     {
                             Lines = s.Lines.Select(l => l.ProductId == productId ? l with { Quantity = quantity } : l)
                                                        .ToImmutableList()
                     });

       public void Remove(int productId) => Set(s =>
              s with { Lines = s.Lines.RemoveAll(l => l.ProductId == productId) });

       public void Replace(IEnumerable<CartLine> lines) => Set(_ => new CartState(lines.ToImmutableList()));
       public void Clear() => Set(_ => CartState.Empty);

       /// <summary>Code promo validé (aperçu panier) : il voyagera jusqu'à la création
       /// de la commande, où le serveur le re-valide et le consomme — seul cet appel
       /// fait foi. Null pour retirer le code.</summary>
       public void SetPromoCode(string? code) => Set(s => s with { PromoCode = code });

       /// <summary>Lecture pour le tunnel de commande (port ICartGateway).</summary>
       public IReadOnlyList<CartLine> Lines => State.Lines;

       /// <summary>Code promo du panier (port ICartGateway).</summary>
       public string? PromoCode => State.PromoCode;
}
