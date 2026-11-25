using System.Collections.Immutable;

namespace ShopPulse.SharedKernel;

public sealed record CartLine(int ProductId, int Quantity);

public sealed record CartState(ImmutableList<CartLine> Lines)
{
       public static CartState Empty { get; } = new(ImmutableList<CartLine>.Empty);

       public int ItemCount() => Lines.Sum(l => l.Quantity);
}
