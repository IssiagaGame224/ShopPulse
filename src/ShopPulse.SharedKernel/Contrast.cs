namespace ShopPulse.SharedKernel;

/// <summary>Calcul du contraste WCAG (utile pour tester les jetons du thème).</summary>
public static class Contrast
{
       private static double Lin(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

       public static double Luminance(byte r, byte g, byte b) =>
              0.2126 * Lin(r / 255.0) + 0.7152 * Lin(g / 255.0) + 0.0722 * Lin(b / 255.0);

       public static double Ratio((byte r, byte g, byte b) a, (byte r, byte g, byte b) b)
       {
              var l1 = Luminance(a.r, a.g, a.b);
              var l2 = Luminance(b.r, b.g, b.b);
              var (hi, lo) = l1 > l2 ? (l1, l2) : (l2, l1);
              return (hi + 0.05) / (lo + 0.05);
       }
}
