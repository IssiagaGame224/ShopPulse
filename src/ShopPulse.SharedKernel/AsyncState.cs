namespace ShopPulse.SharedKernel;

/// <summary>Les trois états d'un chargement : l'état impossible n'est pas représentable.</summary>
public abstract record AsyncState<T>
{
       public sealed record Idle : AsyncState<T>;
       public sealed record Loading : AsyncState<T>;
       public sealed record Success(T Value) : AsyncState<T>;
       public sealed record Failure(Exception Error) : AsyncState<T>;
}
