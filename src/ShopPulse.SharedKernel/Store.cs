namespace ShopPulse.SharedKernel;

/// <summary>Store minimaliste (style Zustand) : un état immuable et des méthodes.</summary>
public abstract class Store<TState> : IObservableState where TState : notnull
{
       protected Store(TState initial) => State = initial;

       public TState State { get; private set; }
       public event Action? Changed;

       /// <summary>Applique une transformation pure. Ne notifie que si l'état a changé.</summary>
       protected void Set(Func<TState, TState> update)
       {
              var next = update(State);
              if (EqualityComparer<TState>.Default.Equals(next, State)) return;
              State = next;
              Changed?.Invoke();
       }
}
