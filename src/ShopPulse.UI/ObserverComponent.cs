using Microsoft.AspNetCore.Components;
using ShopPulse.SharedKernel;

namespace ShopPulse.UI;

/// <summary>Classe de base qui s'abonne et se DÉSABONNE automatiquement des états observables.</summary>
public abstract class ObserverComponent : ComponentBase, IDisposable
{
       private readonly List<IObservableState> _watched = [];

       protected void Watch(IObservableState state)
       {
              state.Changed += OnChanged;
              _watched.Add(state);
       }

       private void OnChanged() => _ = InvokeAsync(StateHasChanged);

       public virtual void Dispose()
       {
              foreach (var s in _watched) s.Changed -= OnChanged;
       }
}
