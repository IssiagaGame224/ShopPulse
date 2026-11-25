namespace ShopPulse.Application;

/// <summary>Paramètres commerciaux de la boutique : UNE SEULE source de vérité pour la
/// règle de livraison et le taux de TVA, partagée par l'API (calcul des commandes,
/// construit depuis la configuration) et le Web (affichage du panier, via /api/store/config).</summary>
public sealed record StoreConfig(
       decimal ShippingFlatRate,     // tarif fixe de livraison (TTC)
       decimal ShippingFreeAbove,    // livraison offerte à partir de ce sous-total (TTC)
       decimal VatRate,              // taux de TVA inclus dans les prix (0.20 = 20 %)
       string Currency)
{
       /// <summary>Frais de port pour un sous-total marchandises donné (TTC).</summary>
       public decimal ShippingFor(decimal subtotal) =>
              subtotal >= ShippingFreeAbove ? 0m : ShippingFlatRate;

       /// <summary>TVA incluse dans un montant TTC (prix B2C : la TVA se déduit, ne s'ajoute pas).</summary>
       public decimal VatIncludedIn(decimal ttc) =>
              Math.Round(ttc * VatRate / (1 + VatRate), 2);
}

public interface IStoreConfigProvider
{
       Task<StoreConfig> GetAsync(CancellationToken ct = default);
}
