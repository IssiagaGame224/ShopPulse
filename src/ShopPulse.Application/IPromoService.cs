using ShopPulse.Domain;

namespace ShopPulse.Application;

/// <summary>Port des codes promo côté Web : validation pour l'aperçu panier
/// (sans consommation) et administration. La remise affichée n'est qu'un aperçu —
/// le serveur re-valide et re-calcule tout à la création de la commande.</summary>
public interface IPromoService
{
       /// <summary>Code utilisable + remise pour ce sous-total ; null si invalide/expiré/épuisé.</summary>
       Task<PromoPreview?> ValidateAsync(string code, decimal subtotal, CancellationToken ct = default);

       Task<IReadOnlyList<PromoCode>> GetAllAsync(CancellationToken ct = default);

       /// <summary>(promo, null) ou (null, message d'erreur).</summary>
       Task<(PromoCode? Promo, string? Error)> CreateAsync(
              string code, PromoKind kind, decimal value, DateTimeOffset? expiresAt, int? maxUses,
              CancellationToken ct = default);

       Task<bool> SetActiveAsync(int id, bool active, CancellationToken ct = default);
}

/// <summary>Aperçu d'un code validé : le type et la valeur permettent au panier de
/// recalculer la remise localement quand les quantités changent (même formule Domain).</summary>
public sealed record PromoPreview(string Code, PromoKind Kind, decimal Value)
{
       public decimal DiscountFor(decimal subtotal) => PromoDiscount.Compute(Kind, Value, subtotal);
}
