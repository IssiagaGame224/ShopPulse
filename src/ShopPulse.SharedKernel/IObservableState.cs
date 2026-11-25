namespace ShopPulse.SharedKernel;

public interface IObservableState
{
       event Action? Changed;
}
