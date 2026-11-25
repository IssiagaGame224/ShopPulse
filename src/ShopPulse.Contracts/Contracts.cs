namespace ShopPulse.Contracts;

/// <summary>Contrats partagés entre l'API et ses clients : client et serveur évoluent sans surprise.</summary>
public sealed record ProductDto(
       int Id,
       string Name,
       string Category,
       decimal Price,
       int Stock,
       string ImageUrl,
       string Description);

public sealed record PageDto<T>(IReadOnlyList<T> Items, string? NextCursor);

public sealed record ProblemInfo(string Title, int Status, string? Detail);

/// <summary>Mise à jour de stock publiée sur le hub SignalR.</summary>
public sealed record StockUpdate(int ProductId, int Stock);

/// <summary>Demande de modification de stock (back-office, exercice du module 6).</summary>
public sealed record SetStockRequest(int ProductId, int Stock);

/// <summary>Décrément de stock transactionnel (achat) : refusé si le stock est insuffisant.</summary>
public sealed record DecrementStockRequest(int ProductId, int Quantity);

/// <summary>Changement de statut d'une commande (back-office) : « Pending », « Paid »,
/// « Shipped » ou « Cancelled » (chaîne pour garder Contracts sans dépendance Domain).</summary>
public sealed record SetOrderStatusRequest(string Status);

/// <summary>Session de paiement renvoyée au navigateur (Stripe.js ou simulateur dev).</summary>
public sealed record PaymentSessionDto(
       string PaymentIntentId,
       string ClientSecret,
       string? PublishableKey,
       bool Live);

/// <summary>Création de commande : les prix et le total sont recalculés côté serveur,
/// seuls produit et quantité sont fournis par le client (jamais de prix venu du navigateur).</summary>
public sealed record PlaceOrderLineRequest(int ProductId, int Quantity);

public sealed record PlaceOrderRequest(
       string Email,
       string FullName,
       string Address,
       string PostalCode,
       IReadOnlyList<PlaceOrderLineRequest> Lines);
