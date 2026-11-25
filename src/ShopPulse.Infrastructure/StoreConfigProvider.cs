using Microsoft.Extensions.Configuration;
using ShopPulse.Application;

namespace ShopPulse.Infrastructure;

/// <summary>Lit les paramètres commerciaux depuis la configuration
/// (Store:Shipping:FlatRate, Store:Shipping:FreeAbove, Store:VatRate, Store:Currency).</summary>
public sealed class StoreConfigProvider(IConfiguration config) : IStoreConfigProvider
{
       public Task<StoreConfig> GetAsync(CancellationToken ct = default) =>
              Task.FromResult(new StoreConfig(
                     config.GetValue("Store:Shipping:FlatRate", 5.90m),
                     config.GetValue("Store:Shipping:FreeAbove", 60m),
                     config.GetValue("Store:VatRate", 0.20m),
                     config["Store:Currency"] ?? "EUR"));
}
