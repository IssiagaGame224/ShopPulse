using Microsoft.Extensions.DependencyInjection;
using ShopPulse.Modularity;
using System.Reflection;

namespace ShopPulse.Features.Checkout;

public sealed class CheckoutModule : IShopModule
{
       public string Name => "Checkout";
       public Assembly UiAssembly => typeof(CheckoutModule).Assembly;

       public void ConfigureServices(IServiceCollection services)
       {
              services.AddScoped<CheckoutMachine>();
       }
}
