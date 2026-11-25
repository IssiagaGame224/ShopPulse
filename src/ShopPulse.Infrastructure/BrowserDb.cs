using Microsoft.JSInterop;

namespace ShopPulse.Infrastructure;

/// <summary>Accès C# à IndexedDB via le module ES wwwroot/js/db.js (servi par l'hôte).</summary>
public sealed class BrowserDb(IJSRuntime js) : IAsyncDisposable
{
       private IJSObjectReference? _mod;

       private async ValueTask<IJSObjectReference> Mod() =>
              _mod ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/db.js");

       public async Task PutAsync<T>(string store, T value) =>
              await (await Mod()).InvokeVoidAsync("put", store, value);

       public async Task<T[]> GetAllAsync<T>(string store) =>
              await (await Mod()).InvokeAsync<T[]>("getAll", store);

       public async Task RemoveAsync(string store, string id) =>
              await (await Mod()).InvokeVoidAsync("remove", store, id);

       public async ValueTask DisposeAsync()
       {
              if (_mod is null) return;
              try { await _mod.DisposeAsync(); } catch (JSDisconnectedException) { }
       }
}
