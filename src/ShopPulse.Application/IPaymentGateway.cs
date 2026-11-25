using ShopPulse.Domain;

namespace ShopPulse.Application;

/// <summary>Session de paiement renvoyée au navigateur : le clientSecret permet à
/// Stripe.js de confirmer le paiement ; PublishableKey est null en mode simulateur.</summary>
public sealed record PaymentSession(
       string PaymentIntentId,
       string ClientSecret,
       string? PublishableKey,
       bool Live);

/// <summary>Port de paiement côté API. Deux implémentations :
/// StripePaymentGateway (clés configurées) et FakePaymentGateway (dev sans clés —
/// la boucle complète reste testable, la confirmation passe par un endpoint dédié).
/// Le montant vient TOUJOURS de la commande persistée, jamais du client.</summary>
public interface IPaymentGateway
{
       /// <summary>false = simulateur de développement (aucune clé Stripe configurée).</summary>
       bool IsLive { get; }

       /// <summary>Clé publique Stripe (pk_test_…/pk_live_…), null en mode simulateur.</summary>
       string? PublishableKey { get; }

       /// <summary>Crée (ou rejoue, idempotence par id de commande) l'intent de paiement.</summary>
       Task<PaymentSession> CreatePaymentAsync(Order order, CancellationToken ct = default);

       /// <summary>Valide la signature du webhook et retourne l'id de la commande payée ;
       /// null si l'événement n'est pas une confirmation. Lève si la signature est invalide.</summary>
       Task<Guid?> HandleWebhookAsync(string payload, string? signatureHeader, CancellationToken ct = default);

       /// <summary>Rembourse intégralement une commande payée (annulation). Idempotent
       /// côté Stripe (clé = id de commande). Sans paiement associé : NothingToRefund.</summary>
       Task<RefundResult> RefundAsync(Order order, CancellationToken ct = default);
}

public enum RefundStatus { Refunded, NothingToRefund, Failed }

public sealed record RefundResult(RefundStatus Status, string? Error = null)
{
       public static readonly RefundResult Ok = new(RefundStatus.Refunded);
       public static readonly RefundResult Nothing = new(RefundStatus.NothingToRefund);
       public static RefundResult Failed(string error) => new(RefundStatus.Failed, error);
}

/// <summary>Port de paiement côté Web : le tunnel demande une session à l'API,
/// puis confirme via Stripe.js (ou le simulateur en dev).</summary>
public interface IPaymentClient
{
       Task<PaymentSession?> CreateSessionAsync(Guid orderId, CancellationToken ct = default);

       /// <summary>Simulateur dev uniquement : confirme le paiement sans Stripe.</summary>
       Task<bool> FakeConfirmAsync(Guid orderId, CancellationToken ct = default);
}
