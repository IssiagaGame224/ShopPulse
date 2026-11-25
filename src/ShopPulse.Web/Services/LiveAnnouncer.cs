using ShopPulse.Application;

namespace ShopPulse.Web.Services;

/// <summary>
/// La règle capitale : la région live existe dans le DOM (MainLayout), vide, avant tout changement.
/// Utilisation : Announcer.Announce("Produit ajouté au panier.");
/// </summary>
public sealed class LiveAnnouncer : ILiveAnnouncer
{
       public string Polite { get; private set; } = "";
       public string Assertive { get; private set; } = "";
       public event Action? Changed;

       public void Announce(string message, bool urgent = false)
       {
              if (urgent) Assertive = message; else Polite = message;
              Changed?.Invoke();
       }
}
