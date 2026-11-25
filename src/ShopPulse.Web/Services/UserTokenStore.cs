using Microsoft.Extensions.Caching.Memory;

namespace ShopPulse.Web.Services;

/// <summary>Pont de jetons pour les circuits Blazor Server : IHttpContextAccessor est null
/// dans un circuit (clic, événement), donc le jeton d'accès capturé à la connexion
/// (OnTokenValidated) est conservé ici par sub, le temps de sa durée de validité.
/// Limite : pas de refresh token — passé ExpiresIn (30 min en dev), les appels API
/// repartent en anonyme jusqu'à la prochaine connexion.</summary>
public sealed class UserTokenStore(IMemoryCache cache)
{
       private static string Key(string sub) => $"oidc-token:{sub}";

       public void Set(string sub, string accessToken, TimeSpan lifetime) =>
              cache.Set(Key(sub), accessToken, lifetime);

       public string? Get(string sub) =>
              cache.TryGetValue(Key(sub), out string? token) ? token : null;
}
