using ShopPulse.Domain;

namespace ShopPulse.Application;

/// <summary>Commande de création de commande, côté client (Web → API).
/// La clé d'idempotence est générée par le client UNE FOIS par tentative d'achat :
/// les reprises (retry réseau, double-clic, rechargement) la réutilisent et ne créent
/// jamais de doublon. UserId n'est JAMAIS fourni par le client HTTP : l'API le déduit
/// du jeton bearer (claim sub) — impossible à falsifier.</summary>
public sealed record PlaceOrderCommand(
       string Email,
       string FullName,
       string Address,
       string PostalCode,
       IReadOnlyList<CartLineRequest> Lines,
       string IdempotencyKey,
       string? UserId = null,
       string? PromoCode = null);

public sealed record CartLineRequest(int ProductId, int Quantity);

public enum OrderPlacementStatus { Created, Replayed, StockConflict, Invalid, Failed }

/// <summary>Résultat de la tentative : la commande (créée ou rejouée), un message
/// destiné à l'utilisateur, et les nouveaux niveaux de stock à publier (temps réel).</summary>
public sealed record OrderPlacement(
       OrderPlacementStatus Status,
       Order? Order,
       string? Message,
       IReadOnlyList<(int ProductId, int NewStock)> StockUpdates)
{
       public static OrderPlacement Created(Order order, IReadOnlyList<(int, int)> updates) =>
              new(OrderPlacementStatus.Created, order, null, updates);
       public static OrderPlacement Replayed(Order order) =>
              new(OrderPlacementStatus.Replayed, order, null, []);
       public static OrderPlacement StockConflict(string message) =>
              new(OrderPlacementStatus.StockConflict, null, message, []);
       public static OrderPlacement Invalid(string message) =>
              new(OrderPlacementStatus.Invalid, null, message, []);
       public static OrderPlacement Failed(string message) =>
              new(OrderPlacementStatus.Failed, null, message, []);
}

/// <summary>Port de création de commande. Implémenté côté Web par OrderApiClient (HTTP)
/// et côté API par EfOrderService (transaction SQL).</summary>
public interface IOrderPlacer
{
       Task<OrderPlacement> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken ct = default);
}

/// <summary>Historique des commandes du client connecté (pages « Mon compte »).</summary>
public interface IOrderHistory
{
       Task<IReadOnlyList<Order>> GetMyOrdersAsync(CancellationToken ct = default);
       Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Suivi invité : consultation d'une commande via un lien signé (sans compte).
/// La signature est calculée côté serveur et envoyée dans l'email de confirmation.</summary>
public interface IOrderTracking
{
       Task<Order?> TrackAsync(Guid id, string signature, CancellationToken ct = default);
}

/// <summary>Envoi des emails transactionnels. Effet de bord : un échec d'envoi ne doit
/// JAMAIS faire échouer l'opération métier (l'appelant journalise et continue).
/// Limite : en production, préférer un pattern Outbox (email garanti, rejouable).</summary>
public interface IOrderEmailSender
{
       Task SendConfirmationAsync(Order order, CancellationToken ct = default);

       /// <summary>Notification de changement de statut (payée, expédiée, annulée).</summary>
       Task SendStatusChangedAsync(Order order, CancellationToken ct = default);
}
