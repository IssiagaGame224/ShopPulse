using System.Collections.Concurrent;

namespace ShopPulse.SharedKernel;

/// <summary>
/// Mini TanStack Query : cache par clé, dédoublonnage des requêtes concurrentes,
/// péremption (staleTime) et invalidation par préfixe.
/// </summary>
public sealed class QueryClient
{
       private sealed class Entry(Func<Task<object?>> start)
       {
              public DateTime CreatedAt { get; } = DateTime.UtcNow;
              public Lazy<Task<object?>> Task { get; } = new(start);
       }

       private readonly ConcurrentDictionary<string, Entry> _cache = new();

       /// <summary>Déclenché après invalidation. Les composants peuvent recharger.</summary>
       public event Action<string>? Invalidated;

       public async Task<T> GetAsync<T>(
              string key, Func<CancellationToken, Task<T>> fetch, TimeSpan staleTime)
       {
              var entry = _cache.AddOrUpdate(
                     key,
                     _ => Create(fetch),
                     (_, old) => IsUsable(old, staleTime) ? old : Create(fetch));

              return (T)(await entry.Task.Value)!;
       }

       // La requête partagée ne dépend du jeton d'aucun appelant : si le premier abandonnait,
       // tous les suivants recevraient une annulation.
       private static Entry Create<T>(Func<CancellationToken, Task<T>> fetch) =>
              new(async () => await fetch(CancellationToken.None));

       private static bool IsUsable(Entry e, TimeSpan staleTime)
       {
              if (DateTime.UtcNow - e.CreatedAt >= staleTime) return false;
              return !(e.Task.IsValueCreated && (e.Task.Value.IsFaulted || e.Task.Value.IsCanceled));
       }

       /// <summary>Supprime toutes les entrées dont la clé commence par le préfixe.</summary>
       public void Invalidate(string keyPrefix)
       {
              foreach (var key in _cache.Keys.Where(k => k.StartsWith(keyPrefix, StringComparison.Ordinal)))
                     _cache.TryRemove(key, out _);

              Invalidated?.Invoke(keyPrefix);
       }
}
