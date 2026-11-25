using System.Net.Http.Json;
using System.Text.Json;

namespace ShopPulse.E2E;

/// <summary>
/// Choix d'un produit EN STOCK au moment du test. Les runs répétés décrémentent le
/// stock : un id fixe finit à 0 et échoue (409 « Stock insuffisant ») sans rapport
/// avec le code testé. Le paramètre <c>skip</c> attribue un produit DIFFÉRENT à
/// chaque classe de test — les classes tournent en parallèle et deux achats sur le
/// même produit fausseraient l'assertion « stock décrémenté d'exactement 1 ».
/// </summary>
internal static class TestCatalog
{
       public static async Task<int> InStockProductIdAsync(HttpClient http, string apiUrl, int skip = 0)
       {
              var catalog = await http.GetFromJsonAsync<List<JsonElement>>(
                     $"{apiUrl}/api/products?page=1&pageSize=100");
              return catalog!.Where(p => p.GetProperty("stock").GetInt32() > 1)
                     .Skip(skip).First()
                     .GetProperty("id").GetInt32();
       }
}
