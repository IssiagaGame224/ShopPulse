using System.Collections.Immutable;
using ShopPulse.Application;
using ShopPulse.SharedKernel;

namespace ShopPulse.Features.Catalog;

public sealed record FavoritesState(ImmutableHashSet<int> Ids)
{
       public static FavoritesState Empty { get; } = new(ImmutableHashSet<int>.Empty);
}

/// <summary>Favoris avec mise à jour optimiste : l'UI répond avant le serveur, on annule par
/// l'opération inverse si l'appel échoue.</summary>
public sealed class FavoritesStore(IFavoriteService api) : Store<FavoritesState>(FavoritesState.Empty)
{
       public event Action<string>? Failed;

       public bool IsFavorite(int id) => State.Ids.Contains(id);

       public async Task ToggleAsync(int id)
       {
              var target = !IsFavorite(id);

              Apply(id, target);                           // 1. mise à jour optimiste

              try
              {
                     await api.SetAsync(id, target);         // 2. confirmation serveur
              }
              catch (Exception)
              {
                     Apply(id, !target);                     // 3. annulation de CETTE modification seulement
                     Failed?.Invoke("Impossible de mettre à jour vos favoris.");
              }
       }

       private void Apply(int id, bool favorite) => Set(s =>
              favorite ? s with { Ids = s.Ids.Add(id) } : s with { Ids = s.Ids.Remove(id) });
}
