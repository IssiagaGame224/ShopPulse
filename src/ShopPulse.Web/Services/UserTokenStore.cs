using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;

namespace ShopPulse.Web.Services;

/// <summary>Pont de jetons pour les circuits Blazor Server : IHttpContextAccessor est null
/// dans un circuit, donc les jetons capturés à la connexion (OnTokenValidated) vivent ici,
/// par sub. Quand l'access token expire, il est RAFRAÎCHI via le refresh token Keycloak
/// (une seule requête en vol par utilisateur) — les appels API ne repassent plus en anonyme
/// au bout de 30 minutes.</summary>
public sealed class UserTokenStore(
       IMemoryCache cache,
       IHttpClientFactory httpFactory,
       IConfiguration config,
       ILogger<UserTokenStore> logger)
{
       private sealed record Entry(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);
       private static string Key(string sub) => $"oidc-token:{sub}";

       // Marge : on rafraîchit 60 s AVANT l'expiration réelle.
       private static readonly TimeSpan _margin = TimeSpan.FromSeconds(60);
       private readonly SemaphoreSlim _refreshLock = new(1, 1);

       public void Set(string sub, string accessToken, string? refreshToken, TimeSpan lifetime) =>
              cache.Set(Key(sub), new Entry(accessToken, refreshToken, DateTimeOffset.UtcNow + lifetime),
                     lifetime + TimeSpan.FromDays(1));   // l'entrée survit à l'access token (refresh possible)

       /// <summary>Jeton d'accès valide pour ce sub : tel quel si frais, rafraîchi sinon.
       /// Null si pas de session ou refresh impossible (→ appel API anonyme).</summary>
       public async Task<string?> GetValidTokenAsync(string sub, CancellationToken ct = default)
       {
              if (!cache.TryGetValue(Key(sub), out Entry? entry) || entry is null)
                     return null;
              if (entry.ExpiresAt - _margin > DateTimeOffset.UtcNow)
                     return entry.AccessToken;
              var refreshToken = entry.RefreshToken;
              if (refreshToken is null)
                     return null;

              await _refreshLock.WaitAsync(ct);
              try
              {
                     // Relecture : un autre appel a peut-être déjà rafraîchi pendant l'attente.
                     if (cache.TryGetValue(Key(sub), out Entry? fresh) && fresh is not null
                            && fresh.ExpiresAt - _margin > DateTimeOffset.UtcNow)
                            return fresh.AccessToken;

                     var http = httpFactory.CreateClient();
                     var response = await http.PostAsync(
                            $"{config["Oidc:Authority"]}/protocol/openid-connect/token",
                            new FormUrlEncodedContent(new Dictionary<string, string>
                            {
                                   ["grant_type"] = "refresh_token",
                                   ["client_id"] = config["Oidc:ClientId"]!,
                                   ["client_secret"] = config["Oidc:ClientSecret"]!,
                                   ["refresh_token"] = refreshToken,
                            }), ct);
                     if (!response.IsSuccessStatusCode)
                     {
                            logger.LogInformation(
                                   "Refresh token refusé pour {Sub} ({Status}) — session à reconnecter.",
                                   sub, (int)response.StatusCode);
                            cache.Remove(Key(sub));
                            return null;
                     }

                     var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
                     if (payload?.AccessToken is null) return null;
                     var lifetime = TimeSpan.FromSeconds(payload.ExpiresIn > 0 ? payload.ExpiresIn : 300);
                     Set(sub, payload.AccessToken, payload.RefreshToken ?? refreshToken, lifetime);
                     logger.LogInformation("Access token rafraîchi pour {Sub} (+{Minutes} min).",
                            sub, (int)lifetime.TotalMinutes);
                     return payload.AccessToken;
              }
              finally
              {
                     _refreshLock.Release();
              }
       }

       private sealed record TokenResponse(
              [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string? AccessToken,
              [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken,
              [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
}
