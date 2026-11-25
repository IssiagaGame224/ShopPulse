using Microsoft.Extensions.DependencyInjection;
using ShopPulse.Modularity;
using System.Reflection;

namespace ShopPulse.Features.Catalog;

public sealed class CatalogModule : IShopModule
{
       public string Name => "Catalog";
       public Assembly UiAssembly => typeof(CatalogModule).Assembly;

       public void ConfigureServices(IServiceCollection services)
       {
              services.AddScoped<ProductStore>();
              services.AddScoped<FavoritesStore>();
       }
}
