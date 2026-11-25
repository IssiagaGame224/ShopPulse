using System.Security.Cryptography;
using System.Text;

namespace ShopPulse.Infrastructure;

/// <summary>Lien de suivi invité : la signature HMAC-SHA256(orderId, clé serveur) rend
/// l'URL indevinable sans session — une « capability URL » classique. La clé vient de
/// Store:TrackingKey (surcharger en production via l'environnement).</summary>
public static class OrderTracking
{
       public const string DefaultDevKey = "dev-tracking-key-shoppulse";

       public static string ComputeSignature(Guid orderId, string key)
       {
              var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key),
                     Encoding.UTF8.GetBytes(orderId.ToString("N")));
              return Convert.ToHexStringLower(hash)[..16];
       }

       public static bool Validate(Guid orderId, string? signature, string key)
       {
              if (signature is null || signature.Length != 16) return false;
              var expected = ComputeSignature(orderId, key);
              // Comparaison en temps constant : pas d'oracle timing sur la signature.
              return CryptographicOperations.FixedTimeEquals(
                     Encoding.UTF8.GetBytes(expected),
                     Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
       }
}
