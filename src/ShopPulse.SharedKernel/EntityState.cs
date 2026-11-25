using System.Collections.Immutable;

namespace ShopPulse.SharedKernel;

/// <summary>État normalisé : chaque entité est stockée une seule fois, indexée par id.</summary>
public sealed record EntityState<T>(ImmutableDictionary<int, T> ById, ImmutableList<int> Ids)
{
       public static EntityState<T> Empty { get; } =
              new(ImmutableDictionary<int, T>.Empty, ImmutableList<int>.Empty);

       public EntityState<T> UpsertMany(IEnumerable<T> items, Func<T, int> key)
       {
              var byId = ById.ToBuilder();
              var ids = Ids.ToBuilder();
              foreach (var item in items)
              {
                     var k = key(item);
                     if (!byId.ContainsKey(k)) ids.Add(k);
                     byId[k] = item;
              }
              return new EntityState<T>(byId.ToImmutable(), ids.ToImmutable());
       }

       public IEnumerable<T> All() => Ids.Select(id => ById[id]);
}
