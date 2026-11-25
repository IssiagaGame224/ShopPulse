using Microsoft.AspNetCore.Authentication;

namespace ShopPulse.Web.Services;

/// <summary>Propage le jeton d'accès Keycloak vers l'API : les appels « api » portent
/// l'identité de l'utilisateur courant. Deux contextes d'exécution :
/// — SSR (pages statiques) : le jeton est relu depuis le cookie chiffré via HttpContext ;
/// — circuit Blazor (clics) : pas de HttpContext — le sub vient de CurrentUserAccessor
///   (renseigné par MainLayout) et le jeton du UserTokenStore.
/// Sans utilisateur connecté, la requête part anonyme (checkout invité, catalogue).</summary>
public sealed class AccessTokenHandler(
       IHttpContextAccessor httpContextAccessor,
       CurrentUserAccessor currentUser,
       UserTokenStore tokenStore) : DelegatingHandler
{
       protected override async Task<HttpResponseMessage> SendAsync(
              HttpRequestMessage request, CancellationToken cancellationToken)
       {
              string? token = null;

              if (httpContextAccessor.HttpContext is { } http)
                     token = await http.GetTokenAsync("access_token");

              if (token is null && currentUser.Sub is { } sub)
                     token = await tokenStore.GetValidTokenAsync(sub, cancellationToken);

              if (token is not null)
                     request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
              return await base.SendAsync(request, cancellationToken);
       }
}
