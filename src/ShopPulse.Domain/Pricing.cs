namespace ShopPulse.Domain;

/// <summary>Règle métier dans le domaine, testable sans interface ni base de données.</summary>
public static class Pricing
{
       public const decimal VatRate = 0.20m;
       public static decimal WithVat(decimal net) => decimal.Round(net * (1 + VatRate), 2);
}
