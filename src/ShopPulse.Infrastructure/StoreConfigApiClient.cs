using ShopPulse.Application;
using System.Net.Http.Json;

namespace ShopPulse.Infrastructure;

/// <summary>Lit les paramètres commerciaux (livraison, TVA) depuis l'API :
/// le panier affiche la MÊME règle que celle appliquée à la création de la commande.</summary>
public sealed class StoreConfigApiClient(IHttpClientFactory factory) : IStoreConfigProvider
{
       public async Task<StoreConfig> GetAsync(CancellationToken ct = default) =>
              await factory.CreateClient("api")
                     .GetFromJsonAsync<StoreConfig>("api/store/config", ct)
              ?? new StoreConfig(5.90m, 60m, 0.20m, "EUR");   // repli cohérent avec les défauts serveur
}
