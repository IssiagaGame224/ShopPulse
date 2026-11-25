using Microsoft.Extensions.DependencyInjection;
using ShopPulse.Modularity;
using System.Reflection;

namespace ShopPulse.Features.Admin;

/// <summary>Back-office (phase 5) : gestion des commandes (statuts) et du catalogue.
/// Pages réservées au rôle « stock-manager » (politique CanManageStock, appliquée
/// au routage par AuthorizeRouteView ET côté API par le JWT).</summary>
public sealed class AdminModule : IShopModule
{
       public string Name => "Admin";
       public Assembly UiAssembly => typeof(AdminModule).Assembly;

       public void ConfigureServices(IServiceCollection services)
       {
              // Les pages consomment IOrderAdmin / IProductAdmin / IProductService,
              // enregistrés par l'hôte (clients API).
       }
}
