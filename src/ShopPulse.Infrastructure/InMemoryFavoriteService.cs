using ShopPulse.Application;

namespace ShopPulse.Infrastructure;

/// <summary>Implémentation simulée pour tester le store. Réglez FailureRate pour simuler
/// des échecs (exercice : une mise à jour sur trois échoue, l'annulation doit s'afficher).</summary>
public sealed class InMemoryFavoriteService : IFavoriteService
{
       /// <summary>Probabilité d'échec entre 0 et 1 (0 par défaut : toujours réussir).</summary>
       public double FailureRate { get; set; }

       private readonly HashSet<int> _favorites = [];
       private readonly Random _rng = new();

       public Task SetAsync(int productId, bool isFavorite, CancellationToken ct = default)
       {
              if (_rng.NextDouble() < FailureRate)
                     throw new InvalidOperationException("Échec simulé de l'API favoris.");

              if (isFavorite) _favorites.Add(productId);
              else _favorites.Remove(productId);
              return Task.CompletedTask;
       }
}
