namespace ShopPulse.Domain;

/// <summary>Cycle de vie d'une commande. Pending = reçue, en attente de paiement
/// (le paiement réel arrive en phase 6 ; le back-office gère les transitions en phase 5).</summary>
public enum OrderStatus { Pending, Paid, Shipped, Cancelled }

/// <summary>Ligne de commande : instantané du produit AU MOMENT de l'achat
/// (nom et prix copiés — une commande historique ne doit jamais changer
/// si le catalogue évolue).</summary>
public record OrderLine(int ProductId, string ProductName, decimal UnitPrice, int Quantity)
{
       public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>Commande. Les prix sont TOUJOURS recalculés côté serveur à la création ;
/// l'adresse et l'email sont des instantanés (le client peut changer ensuite).
/// UserId : sub Keycloak du client connecté (null = commande invitée).
/// PaymentIntentId : référence du paiement (Stripe pi_… ou fake_… en dev).
/// ShippingCost et TaxRate sont des instantanés commerciaux : la commande historique
/// ne change pas si la boutique modifie ses tarifs ensuite.</summary>
public record Order(
       Guid Id,
       string Email,
       string FullName,
       string Address,
       string PostalCode,
       string Currency,
       OrderStatus Status,
       DateTimeOffset CreatedAt,
       IReadOnlyList<OrderLine> Lines,
       string? UserId = null,
       string? PaymentIntentId = null,
       decimal ShippingCost = 0m,
       decimal TaxRate = 0.20m,
       string? PromoCode = null,
       decimal Discount = 0m)
{
       /// <summary>Marchandises TTC (somme des lignes), avant remise.</summary>
       public decimal Subtotal => Lines.Sum(l => l.LineTotal);

       /// <summary>TVA incluse dans le total (prix B2C : déduite, pas ajoutée).
       /// La livraison est un service TTC au même taux dans ce modèle simple.</summary>
       public decimal TaxAmount => Math.Round((Subtotal - Discount + ShippingCost) * TaxRate / (1 + TaxRate), 2);

       /// <summary>Total à payer : marchandises − remise + livraison, TTC.</summary>
       public decimal Total => Subtotal - Discount + ShippingCost;
}
