using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ShopPulse.Modularity;

public interface IShopModule
{
       string Name { get; }
       Assembly UiAssembly { get; }  // pour le routage

       void ConfigureServices(IServiceCollection services);
}
