using ShopPulse.SharedKernel;
using ShopPulse.UI.Resources;
using System.Xml.Linq;
using Xunit;

namespace ShopPulse.Architecture.Tests;

/// <summary>Module 8 : pluriels CLDR, contraste WCAG des jetons, parité des clés de traduction.</summary>
public class Module8Tests
{
       // Exercice 4 (module 8) : les six catégories de l'arabe.
       [Theory]
       [InlineData(0, PluralCategory.Zero)]
       [InlineData(1, PluralCategory.One)]
       [InlineData(2, PluralCategory.Two)]
       [InlineData(5, PluralCategory.Few)]
       [InlineData(11, PluralCategory.Many)]
       [InlineData(100, PluralCategory.Other)]
       public void Pluriel_arabe(int n, PluralCategory attendu)
              => Assert.Equal(attendu, PluralRules.For("ar", n));

       [Theory]
       [InlineData(0, PluralCategory.One)]   // français : 0 et 1 au singulier
       [InlineData(1, PluralCategory.One)]
       [InlineData(2, PluralCategory.Other)]
       public void Pluriel_français(int n, PluralCategory attendu)
              => Assert.Equal(attendu, PluralRules.For("fr", n));

       [Fact]   // Exercice 6 : le jeton --sp-color-primary sur blanc respecte AA (4,5:1).
       public void Primary_on_white_meets_AA()
              => Assert.True(Contrast.Ratio((11, 95, 255), (255, 255, 255)) >= 4.5);

       // Exercice 5 (module 8) — parité des traductions, en deux volets :
       // 1) les clés NON plurielles sont identiques dans toutes les langues ;
       // 2) chaque langue possède les formes de pluriel QUE ses règles produisent
       //    (l'arabe a six formes, le français deux — une clé .two en français serait un mort).
       [Fact]
       public void Traductions_parfaites_entre_langues()
       {
              var keys = new[] { "fr", "en", "ar" }.ToDictionary(l => l, Keys);
              var frNonPluriel = keys["fr"].Where(k => PrefixePluriel(k) is null).ToHashSet();

              foreach (var (lang, autres) in keys)
              {
                     var nonPluriel = autres.Where(k => PrefixePluriel(k) is null).ToHashSet();
                     Assert.Empty(frNonPluriel.Except(nonPluriel));        // clés manquantes
                     Assert.Empty(nonPluriel.Except(frNonPluriel));        // clés orphelines

                     var prefixes = autres.Select(PrefixePluriel).Where(p => p is not null).Cast<string>().ToHashSet();
                     foreach (var p in prefixes)
                            foreach (var c in Categories(lang))             // complétude des formes
                                   Assert.Contains($"{p}.{c}", autres);
              }
       }

       private static readonly string[] CategoriesParLangue = ["zero", "one", "two", "few", "many", "other"];

       private static string? PrefixePluriel(string key) =>
              CategoriesParLangue.FirstOrDefault(c => key.EndsWith("." + c, StringComparison.Ordinal)) is { } c
                     ? key[..^(c.Length + 1)]
                     : null;

       private static HashSet<string> Categories(string lang) =>
              Enumerable.Range(0, 101).Select(n => PluralRules.For(lang, n).ToString().ToLowerInvariant()).ToHashSet();

       private static HashSet<string> Keys(string lang)
       {
              var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                     "../../../../..", "src", "ShopPulse.UI", "Resources"));
              return XDocument.Load(Path.Combine(dir, $"SharedResource.{lang}.resx"))
                     .Descendants("data").Select(d => (string)d.Attribute("name")!).ToHashSet();
       }
}
