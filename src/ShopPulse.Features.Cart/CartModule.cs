using Microsoft.Extensions.DependencyInjection;
using ShopPulse.Modularity;
using ShopPulse.SharedKernel;
using System.Reflection;

namespace ShopPulse.Features.Cart;

public sealed class CartModule : IShopModule
{
       public string Name => "Cart";
       public Assembly UiAssembly => typeof(CartModule).Assembly;

       public void ConfigureServices(IServiceCollection services)
       {
              services.AddScoped<CartStore>();
              // Le panier est joignable depuis n'importe quelle feature via le port partagé.
              services.AddScoped<ICartGateway>(sp => sp.GetRequiredService<CartStore>());
       }
}
