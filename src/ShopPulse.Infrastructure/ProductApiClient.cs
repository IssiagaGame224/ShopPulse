using ShopPulse.Application;
using ShopPulse.Domain;
using System.Net.Http.Json;

namespace ShopPulse.Infrastructure;

/// <summary>
/// IProductService branché sur l'API HTTP. AddHttpClient gère le pool de connexions
/// et la rotation DNS : ne jamais faire new HttpClient() par appel.
/// La pagination et les filtres sont exécutés côté serveur (SQL) : le client ne
/// télécharge que la page demandée, jamais le catalogue complet.
/// </summary>
public sealed class ProductApiClient(HttpClient http) : IProductService
{
       public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
              => await http.GetFromJsonAsync<List<Product>>("api/products", ct) ?? [];

       public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
              => await http.GetFromJsonAsync<Product>($"api/products/{id}", ct);

       public async Task<PagedResult<Product>> GetPageAsync(
              int skip, int take, string? search, string? category = null, CancellationToken ct = default)
       {
              var url = $"api/products/page?skip={skip}&take={take}";
              if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
              if (!string.IsNullOrWhiteSpace(category)) url += $"&category={Uri.EscapeDataString(category)}";
              return await http.GetFromJsonAsync<PagedResult<Product>>(url, ct)
                     ?? new PagedResult<Product>([], 0);
       }

       public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default)
              => await http.GetFromJsonAsync<List<string>>("api/categories", ct) ?? [];
}
