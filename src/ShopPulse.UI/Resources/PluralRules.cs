namespace ShopPulse.UI.Resources;

/// <summary>Catégories de pluriel CLDR (6 formes en arabe).</summary>
public enum PluralCategory { Zero, One, Two, Few, Many, Other }

public static class PluralRules
{
       public static PluralCategory For(string lang, int n) => lang switch
       {
              "fr" => n is 0 or 1 ? PluralCategory.One : PluralCategory.Other,   // 0 et 1 : singulier
              "en" => n == 1 ? PluralCategory.One : PluralCategory.Other,
              "ar" => Arabic(n),
              _ => PluralCategory.Other
       };

       private static PluralCategory Arabic(int n)
       {
              if (n == 0) return PluralCategory.Zero;
              if (n == 1) return PluralCategory.One;
              if (n == 2) return PluralCategory.Two;
              var m = n % 100;
              if (m is >= 3 and <= 10) return PluralCategory.Few;
              if (m is >= 11 and <= 99) return PluralCategory.Many;
              return PluralCategory.Other;
       }
}
