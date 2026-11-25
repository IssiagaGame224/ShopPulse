using System.Reflection;

namespace ShopPulse.Modularity;

/// <summary>
/// Charge des modules d'un dossier SANS référence directe. Sécurité : une DLL chargée
/// s'exécute avec tous les droits du processus — ne charger que des assemblies contrôlées
/// (signées, dossier protégé en écriture). AssemblyLoadContext pour isoler les dépendances.
/// </summary>
public static class PluginLoader
{
       public static IReadOnlyList<IShopModule> Load(string folder)
       {
              var result = new List<IShopModule>();
              if (!Directory.Exists(folder)) return result;

              foreach (var dll in Directory.GetFiles(folder, "ShopPulse.Plugin.*.dll"))
              {
                     var asm = Assembly.LoadFrom(dll);
                     foreach (var type in asm.GetTypes()
                            .Where(t => typeof(IShopModule).IsAssignableFrom(t) && !t.IsAbstract))
                     {
                            result.Add((IShopModule)Activator.CreateInstance(type)!);
                     }
              }
              return result;
       }
}
