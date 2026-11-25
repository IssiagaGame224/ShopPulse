using System.Security.Cryptography;

namespace ShopPulse.Web.Services;

/// <summary>
/// En-têtes de sécurité sur toutes les réponses + CSP avec nonce par requête.
/// La CSP part en Report-Only (module 7, exercice 1) : relever les violations,
/// puis passer au mode bloquant en renommant l'en-tête.
/// </summary>
public static class SecurityHeaders
{
       public const string CspNonceItemKey = "csp-nonce";

       public static string? GetCspNonce(this HttpContext ctx) =>
              ctx.Items[CspNonceItemKey] as string;

       public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
              app.Use(async (ctx, next) =>
              {
                     var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
                     ctx.Items[CspNonceItemKey] = nonce;

                     var h = ctx.Response.Headers;
                     h["X-Content-Type-Options"] = "nosniff";
                     h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                     h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                     h["Cross-Origin-Opener-Policy"] = "same-origin";
                     h["Cross-Origin-Resource-Policy"] = "same-origin";
                     h["X-Frame-Options"] = "DENY";

                     // CSP APPLIQUÉE (plus Report-Only) : les E2E Playwright vérifient
                     // qu'aucune ressource légitime n'est bloquée (scripts nonce/SRI,
                     // Stripe, WebSocket SignalR, styles bootstrap inline).
                     h["Content-Security-Policy"] =
                            "default-src 'self'; " +
                            // js.stripe.com : Stripe.js (phase 6) — chargé dynamiquement par l'interop.
                            $"script-src 'self' 'nonce-{nonce}' https://js.stripe.com; " +
                            "style-src 'self' 'unsafe-inline'; " +   // bootstrap/styles inline du modèle ; à resserrer
                            "img-src 'self' data:; " +
                            "connect-src 'self' ws: wss: https://api.stripe.com; " +   // ws:/wss: = circuit SignalR (dev en clair)
                            "frame-src https://js.stripe.com https://hooks.stripe.com; " +   // iframes 3DS
                            "frame-ancestors 'none'; " +
                            "base-uri 'self'; form-action 'self'; object-src 'none'";

                     await next();
              });
}
