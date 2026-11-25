using static ShopPulse.Features.Checkout.CheckoutStep;
using static ShopPulse.Features.Checkout.CheckoutEvent;
using ShopPulse.SharedKernel;

namespace ShopPulse.Features.Checkout;

public enum CheckoutStep { Cart, Shipping, Payment, Confirmed, Failed }
public enum CheckoutEvent { Next, Back, Pay, PaymentFailed, Retry }

/// <summary>Machine à états du tunnel de commande : les transitions interdites lèvent.</summary>
public sealed class CheckoutMachine : IObservableState
{
       private static readonly Dictionary<(CheckoutStep, CheckoutEvent), CheckoutStep> Transitions = new()
       {
              [(Cart,     Next)]  = Shipping,
              [(Shipping, Back)]  = Cart,
              [(Shipping, Next)]  = Payment,
              [(Payment, Back)]   = Shipping,
              [(Payment, Pay)]    = Confirmed,
              [(Payment, PaymentFailed)] = Failed,
              [(Failed, Retry)]   = Payment,
       };

       public CheckoutStep Step { get; private set; } = Cart;
       public event Action? Changed;

       public bool CanFire(CheckoutEvent e) => Transitions.ContainsKey((Step, e));

       public void Fire(CheckoutEvent e)
       {
              if (!Transitions.TryGetValue((Step, e), out var next))
                     throw new InvalidOperationException($"« {e} » est interdit depuis l'étape « {Step} ».");

              Step = next;
              Changed?.Invoke();
       }
}
