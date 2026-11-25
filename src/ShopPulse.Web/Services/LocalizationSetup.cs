using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Globalization;

namespace ShopPulse.Web.Services;

/// <summary>
/// Locale routing : /fr/..., /en/..., /ar/... Le préfixe est retiré via PathBase et fixe la
/// culture — les routes des pages n'ont pas à changer. La racine redirige selon Accept-Language.
/// Ne jamais rediriger /api ni /_blazor.
/// </summary>
public static class LocalizationSetup
{
       private static readonly string[] Supported = ["fr", "en", "ar"];

       public static IApplicationBuilder UseLocalePrefix(this IApplicationBuilder app) =>
              app.Use(async (ctx, next) =>
              {
                     var path = ctx.Request.Path;

                     // Jamais : endpoints techniques et API.
                     if (path.StartsWithSegments("/api") || path.StartsWithSegments("/_blazor") ||
                            path.StartsWithSegments("/hubs") || path.StartsWithSegments("/_framework"))
                     {
                            await next();
                            return;
                     }

                     var segments = path.Value?.Split('/', 3, StringSplitOptions.RemoveEmptyEntries) ?? [];

                     if (segments.Length > 0 && Supported.Contains(segments[0]))
                     {
                            var lang = segments[0];

                            ctx.Request.PathBase = ctx.Request.PathBase.Add("/" + lang);   // préfixe → PathBase
                            ctx.Request.Path = "/" + (segments.Length > 1 ? string.Join('/', segments.Skip(1)) : "");

                            var culture = new CultureInfo(lang);
                            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
                     }
                     else if (path == "/")
                     {
                            ctx.Response.Redirect("/" + PreferredLanguage(ctx) + "/");    // selon Accept-Language
                            return;
                     }

                     await next();
              });

       private static string PreferredLanguage(HttpContext ctx)
       {
              var accepted = ctx.Request.GetTypedHeaders().AcceptLanguage
                     .OrderByDescending(a => a.Quality ?? 1)
                     .Select(a => a.Value.Value?.Split('-')[0]);

              return accepted.FirstOrDefault(Supported.Contains) ?? "fr";
       }
}
