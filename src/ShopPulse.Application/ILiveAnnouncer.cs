namespace ShopPulse.Application;

/// <summary>Port du region live : annonce aux lecteurs d'écran les changements sans focus.</summary>
public interface ILiveAnnouncer
{
       event Action? Changed;
       string Polite { get; }
       string Assertive { get; }
       void Announce(string message, bool urgent = false);
}
