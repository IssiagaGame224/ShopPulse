namespace ShopPulse.SharedKernel;

/// <summary>Thème de l'interface, offert à toute l'arborescence via CascadingValue.</summary>
public sealed class ThemeState : Store<string>
{
       public ThemeState() : base("system") { }   // system | light | dark

       public string Mode => State;
       public void Set(string mode) => Set(s => mode);
}
