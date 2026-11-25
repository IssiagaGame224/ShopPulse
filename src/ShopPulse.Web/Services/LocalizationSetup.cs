using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Globalization;

namespace ShopPulse.Web.Services;

/// <summary>
/// Locale routing : /fr/..., /en/..., /ar/... Le préfixe est retiré via PathBase et fixe la
/// culture — les routes des pages n'ont pas à changer. La racine redirige selon le cookie
/// sp_locale puis Accept-Language ; les URL sans préfixe (liens absolus du layout) ne sont
/// jamais redirigées mais reçoivent la culture mémorisée. Seule la racine redirige —
/// jamais /api, /_blazor ni les assets.
/// </summary>
public static class LocalizationSetup
{
       private static readonly string[] Supported = ["fr", "en", "ar"];

       public static IApplicationBuilder UseLocalePrefix(this IApplicationBuilder app) =>
              app.Use(async (ctx, next) =>
              {
                     var path = ctx.Request.Path;
                     var segments = path.Value?.Split('/', 3, StringSplitOptions.RemoveEmptyEntries) ?? [];

                     if (segments.Length > 0 && Supported.Contains(segments[0]))
                     {
                            var lang = segments[0];

                            ctx.Request.PathBase = ctx.Request.PathBase.Add("/" + lang);   // préfixe → PathBase
                            ctx.Request.Path = "/" + (segments.Length > 1 ? string.Join('/', segments.Skip(1)) : "");

                            var culture = new CultureInfo(lang);
                            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;

                            // Mémorise la locale : les liens absolus du layout (/cart,
                            // /admin/…) perdent le préfixe — sans ce cookie, la culture
                            // retombait sur celle du système hôte (fr-FR en dev, invariante
                            // en conteneur → clés de traduction brutes, cf. docs/24).
                            ctx.Response.Cookies.Append("sp_locale", lang, new CookieOptions
                            {
                                   Path = "/",
                                   SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                                   Secure = ctx.Request.IsHttps,
                                   HttpOnly = true,
                                   IsEssential = true,
                            });
                     }
                     else if (path == "/")
                     {
                            ctx.Response.Redirect("/" + PreferredLanguage(ctx) + "/");    // selon cookie puis Accept-Language
                            return;
                     }
                     else
                     {
                            // URL sans préfixe : jamais de redirection (risque pour
                            // /signin-oidc, /_blazor, les assets), mais la culture suit la
                            // locale mémorisée — y compris pour le circuit SignalR, dont les
                            // requêtes /_blazor passent par ici.
                            var culture = new CultureInfo(PreferredLanguage(ctx));
                            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
                     }

                     await next();
              });

       private static string PreferredLanguage(HttpContext ctx)
       {
              if (ctx.Request.Cookies["sp_locale"] is { } saved && Supported.Contains(saved))
                     return saved;

              var accepted = ctx.Request.GetTypedHeaders().AcceptLanguage
                     .OrderByDescending(a => a.Quality ?? 1)
                     .Select(a => a.Value.Value?.Split('-')[0]);

              return accepted.FirstOrDefault(Supported.Contains) ?? "fr";
       }
}
