using System.Collections.Concurrent;

namespace ShopPulse.SharedKernel;

/// <summary>
/// Stale-while-revalidate côté serveur : retourne la valeur en cache tout de suite et
/// lance un rafraîchissement en arrière-plan si elle est périmée (HybridCache ne fait
/// pas de SWR automatique).
/// </summary>
public sealed class SwrCache
{
       private sealed record Entry(object Value, DateTime FetchedAt);

       private readonly ConcurrentDictionary<string, Entry> _store = new();
       private readonly ConcurrentDictionary<string, Task> _refreshing = new();

       /// <summary>Retourne la valeur en cache tout de suite et lance un rafraîchissement si périmée.</summary>
       public async Task<T> GetAsync<T>(
              string key, Func<CancellationToken, Task<T>> fetch,
              TimeSpan fresh, TimeSpan maxStale, Action<T>? onRefreshed = null)
       {
              if (_store.TryGetValue(key, out var entry))
              {
                     var age = DateTime.UtcNow - entry.FetchedAt;
                     if (age < fresh) return (T)entry.Value;                          // frais

                     if (age < maxStale)                                              // périmé mais utilisable
                     {
                            _ = _refreshing.GetOrAdd(key, _ => RefreshAsync(key, fetch, onRefreshed));
                            return (T)entry.Value;
                     }
              }

              var value = await fetch(CancellationToken.None);                // absent ou trop vieux
              _store[key] = new Entry(value!, DateTime.UtcNow);
              return value;
       }

       private async Task RefreshAsync<T>(string key, Func<CancellationToken, Task<T>> fetch, Action<T>? cb)
       {
              try
              {
                     var v = await fetch(CancellationToken.None);
                     _store[key] = new Entry(v!, DateTime.UtcNow);
                     cb?.Invoke(v);
              }
              catch { /* on garde l'ancienne valeur */ }
              finally { _refreshing.TryRemove(key, out _); }
       }
}
