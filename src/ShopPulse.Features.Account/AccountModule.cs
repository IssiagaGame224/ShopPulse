using Microsoft.Extensions.DependencyInjection;
using ShopPulse.Modularity;
using System.Reflection;

namespace ShopPulse.Features.Account;

/// <summary>Feature « Mon compte » (phase 4) : profil, historique et détail des commandes.
/// Pages statiques (SSR) protégées par [Authorize] — aucun état interactif à enregistrer.</summary>
public sealed class AccountModule : IShopModule
{
       public string Name => "Account";
       public Assembly UiAssembly => typeof(AccountModule).Assembly;

       public void ConfigureServices(IServiceCollection services)
       {
              // Les pages consomment IOrderHistory, enregistré par l'hôte (client API).
       }
}
