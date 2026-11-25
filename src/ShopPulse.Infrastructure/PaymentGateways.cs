using ShopPulse.Application;
using ShopPulse.Domain;
using Stripe;

namespace ShopPulse.Infrastructure;

/// <summary>Gateway Stripe (mode test ou live selon les clés). Points clés :
/// — le montant vient de la commande persistée (Total recalculé serveur, phase 3) ;
/// — clé d'idempotence Stripe = id de commande : recréer une session après un retry
///   réseau rend le MÊME PaymentIntent, jamais un doublon ;
/// — la confirmation de paiement n'est JAMAIS lue depuis le navigateur : seul le
///   webhook signé (HandleWebhookAsync) fait foi.</summary>
public sealed class StripePaymentGateway : IPaymentGateway
{
       private readonly string _webhookSecret;
       private readonly PaymentIntentService _intents;

       public StripePaymentGateway(string secretKey, string publishableKey, string webhookSecret)
       {
              PublishableKey = publishableKey;
              _webhookSecret = webhookSecret;
              _intents = new PaymentIntentService(new StripeClient(secretKey));
       }

       public bool IsLive => true;
       public string? PublishableKey { get; }

       public async Task<PaymentSession> CreatePaymentAsync(Order order, CancellationToken ct = default)
       {
              var options = new PaymentIntentCreateOptions
              {
                     // Stripe compte dans la plus petite unité (centimes).
                     Amount = (long)Math.Round(order.Total * 100),
                     Currency = order.Currency.ToLowerInvariant(),
                     AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
                     Metadata = new Dictionary<string, string> { ["orderId"] = order.Id.ToString() },
              };
              var intent = await _intents.CreateAsync(options,
                     new RequestOptions { IdempotencyKey = order.Id.ToString("N") }, ct);
              return new PaymentSession(intent.Id, intent.ClientSecret!, PublishableKey, Live: true);
       }

       public Task<Guid?> HandleWebhookAsync(string payload, string? signatureHeader, CancellationToken ct = default)
       {
              // Lève StripeException si la signature est invalide → l'endpoint répond 400.
              var stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _webhookSecret);
              if (stripeEvent.Type == EventTypes.PaymentIntentSucceeded
                     && stripeEvent.Data.Object is PaymentIntent intent
                     && intent.Metadata.TryGetValue("orderId", out var raw)
                     && Guid.TryParse(raw, out var orderId))
              {
                     return Task.FromResult<Guid?>(orderId);
              }
              return Task.FromResult<Guid?>(null);   // événement ignoré (accusé 200 quand même)
       }
}

/// <summary>Simulateur de développement (aucune clé Stripe configurée) : la boucle
/// commande → paiement → Paid reste entièrement testable. La session est déterministe
/// par commande (rejouable) ; la confirmation passe par l'endpoint dédié /fake-confirm,
/// mappé UNIQUEMENT quand ce gateway est actif.</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
       public bool IsLive => false;
       public string? PublishableKey => null;

       public Task<PaymentSession> CreatePaymentAsync(Order order, CancellationToken ct = default) =>
              Task.FromResult(new PaymentSession(
                     $"fake_{order.Id:N}", $"fake_secret_{order.Id:N}", null, Live: false));

       public Task<Guid?> HandleWebhookAsync(string payload, string? signatureHeader, CancellationToken ct = default) =>
              Task.FromResult<Guid?>(null);
}
