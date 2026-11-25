using Ganss.Xss;

namespace ShopPulse.Infrastructure;

/// <summary>
/// Nettoyage du HTML qu'on veut AFFICHER (distinguer : valider = refuser,
/// encoder = selon le contexte, nettoyer = retirer le dangereux).
/// Côté Razor : @((MarkupString)Sanitizer.Clean(html)) — MarkupString n'est
/// utilisé qu'avec ce service.
/// </summary>
public sealed class HtmlSanitizerService
{
       private readonly HtmlSanitizer _s = Create();

       private static HtmlSanitizer Create()
       {
              var s = new HtmlSanitizer();
              s.AllowedTags.Clear();
              foreach (var t in new[] { "p", "b", "i", "em", "strong", "ul", "ol", "li", "a", "br" })
                     s.AllowedTags.Add(t);
              s.AllowedAttributes.Clear();
              s.AllowedAttributes.Add("href");
              s.AllowedSchemes.Clear();
              s.AllowedSchemes.Add("https");
              return s;
       }

       public string Clean(string? html) => _s.Sanitize(html ?? "");
}
