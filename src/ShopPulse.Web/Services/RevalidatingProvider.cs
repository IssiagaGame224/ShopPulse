using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace ShopPulse.Web.Services;

/// <summary>
/// Dans un circuit interactif, la requête HTTP d'authentification n'est plus rejouée :
/// ce provider revalide périodiquement l'état d'authentification (concept Session Timeout).
/// </summary>
public sealed class RevalidatingProvider(ILoggerFactory loggerFactory)
       : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
       protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(5);

       protected override Task<bool> ValidateAuthenticationStateAsync(
              AuthenticationState state, CancellationToken cancellationToken)
       {
              // En production : vérifier l'expiration du jeton côté serveur (GetTokenAsync + introspection
              // ou claim "session_expires"). Ici : une session dont l'identité est établie reste valide
              // jusqu'au prochain rejet par le cookie (expiration d'inactivité / absolue).
              return Task.FromResult(state.User.Identity?.IsAuthenticated == true);
       }
}
