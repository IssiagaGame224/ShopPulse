using Microsoft.Extensions.Localization;
using System.Globalization;

namespace ShopPulse.UI.Resources;

public static class LocalizerExtensions
{
       /// <summary>Choisit la clé de pluriel selon la langue : L.Plural("Cart.Items", n).</summary>
       public static string Plural(this IStringLocalizer l, string key, int n)
       {
              var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
              var cat = PluralRules.For(lang, n).ToString().ToLowerInvariant();
              var s = l[$"{key}.{cat}", n];
              return s.ResourceNotFound ? l[$"{key}.other", n] : s;
       }
}
