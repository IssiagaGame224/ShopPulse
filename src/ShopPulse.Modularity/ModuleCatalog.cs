using System.Reflection;

namespace ShopPulse.Modularity;

public sealed class ModuleCatalog(IEnumerable<IShopModule> modules)
{
       public IReadOnlyList<IShopModule> Modules { get; } = modules.ToList();
       public Assembly[] Assemblies => Modules.Select(m => m.UiAssembly).ToArray();
}
