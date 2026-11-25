namespace ShopPulse.Web.Services;

/// <summary>Sub Keycloak de l'utilisateur du scope courant (circuit ou requête SSR).
/// Renseigné par MainLayout (qui reçoit l'AuthenticationState en cascade, y compris
/// dans les circuits) : AuthenticationStateProvider ne peut pas être appelé depuis
/// un DelegatingHandler, hors du scope d'un composant.</summary>
public sealed class CurrentUserAccessor
{
       public string? Sub { get; set; }
}
