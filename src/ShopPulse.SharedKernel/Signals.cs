namespace ShopPulse.SharedKernel;

/// <summary>Valeur réactive (modèle "signals") : notifie quand la valeur change.</summary>
public sealed class Signal<T>(T initial) : IObservableState
{
       private T _value = initial;
       public event Action? Changed;

       public T Value
       {
              get => _value;
              set
              {
                     if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                     _value = value;
                     Changed?.Invoke();
              }
       }
}

/// <summary>Valeur calculée à partir de dépendances observables.</summary>
public sealed class Computed<T> : IObservableState, IDisposable
{
       private readonly Func<T> _compute;
       private readonly IObservableState[] _deps;
       private T _cached;

       public Computed(Func<T> compute, params IObservableState[] deps)
       {
              _compute = compute;
              _deps = deps;
              _cached = compute();
              foreach (var d in deps) d.Changed += Recompute;
       }

       public event Action? Changed;
       public T Value => _cached;

       private void Recompute()
       {
              var next = _compute();
              if (EqualityComparer<T>.Default.Equals(next, _cached)) return;
              _cached = next;
              Changed?.Invoke();
       }

       public void Dispose() { foreach (var d in _deps) d.Changed -= Recompute; }
}
