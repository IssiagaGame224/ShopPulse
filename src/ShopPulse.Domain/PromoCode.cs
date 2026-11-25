namespace ShopPulse.Domain;

/// <summary>Type de remise : pourcentage du sous-total, ou montant fixe (TTC).</summary>
public enum PromoKind { Percent, Amount }

/// <summary>Code promo. La consommation (UsedCount) est incrémentée dans la transaction
/// de la commande, par un UPDATE conditionnel atomique — deux commandes simultanées
/// ne peuvent pas dépasser MaxUses.</summary>
public record PromoCode(
       int Id,
       string Code,                 // stocké en majuscules
       PromoKind Kind,
       decimal Value,               // % (1-100) ou montant TTC selon Kind
       DateTimeOffset? ExpiresAt,
       int? MaxUses,
       int UsedCount,
       bool Active)
{
       /// <summary>Le code peut-il être utilisé maintenant ? (la base re-vérifie à la consommation)</summary>
       public bool IsUsable(DateTimeOffset now) =>
              Active
              && (ExpiresAt is null || ExpiresAt > now)
              && (MaxUses is null || UsedCount < MaxUses);
}

/// <summary>Calcul de la remise — UNE SEULE source de vérité, partagée par le panier
/// (affichage), l'endpoint de validation et la création de commande (autorité).</summary>
public static class PromoDiscount
{
       public static decimal Compute(PromoKind kind, decimal value, decimal subtotal) =>
              kind == PromoKind.Percent
                     ? Math.Round(subtotal * value / 100, 2)
                     : Math.Min(value, subtotal);   // jamais de remise > sous-total
}
