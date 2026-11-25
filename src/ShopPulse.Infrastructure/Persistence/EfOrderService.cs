using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShopPulse.Application;
using ShopPulse.Domain;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Création de commandes côté API. Garanties :
/// — prix TOUJOURS recalculés depuis la base (le navigateur n'envoie que produit + quantité) ;
/// — transaction unique : insertion de la commande + décréments de stock, tout ou rien ;
/// — idempotence : la clé unique fait qu'une reprise (retry, double-clic, rechargement)
///   rejoue la commande existante sans doublon ni double décrément — y compris sous
///   requêtes concurrentes (la violation d'unicité PostgreSQL sert de filet).</summary>
public class EfOrderService(ShopPulseDbContext db, EfProductService products,
       IStoreConfigProvider storeConfig, EfPromoService promos) : IOrderPlacer, IOrderAdmin
{
       public async Task<OrderPlacement> PlaceOrderAsync(PlaceOrderCommand cmd, CancellationToken ct = default)
       {
              // Validation serveur (le navigateur n'est jamais digne de confiance).
              if (cmd.Lines.Count == 0)
                     return OrderPlacement.Invalid("Le panier est vide.");
              if (cmd.Lines.Any(l => l.Quantity <= 0))
                     return OrderPlacement.Invalid("Les quantités doivent être positives.");

              // Rejeu séquentiel : la clé a déjà servi → on renvoie la commande existante.
              var existing = await FindByIdempotencyKeyAsync(cmd.IdempotencyKey, ct);
              if (existing is not null)
                     return OrderPlacement.Replayed(existing);

              // Prix serveur : lecture des produits en base, jamais ceux du client.
              var productIds = cmd.Lines.Select(l => l.ProductId).Distinct().ToList();
              var catalog = await db.Products.AsNoTracking()
                     .Where(p => productIds.Contains(p.Id)).ToListAsync(ct);
              var unknown = productIds.Except(catalog.Select(p => p.Id)).ToList();
              if (unknown.Count > 0)
                     return OrderPlacement.Invalid($"Produit inconnu : {string.Join(", ", unknown)}.");

              // Tout ou rien : sans CommitAsync, le Dispose de la transaction annule
              // aussi les décréments déjà faits (rollback).
              await using var tx = await db.Database.BeginTransactionAsync(ct);

              var updates = new List<(int, int)>();
              foreach (var line in cmd.Lines)
              {
                     // UPDATE conditionnel atomique, dans la MÊME transaction (Npgsql
                     // enrôle automatiquement les commandes de la connexion).
                     var newStock = await products.TryDecrementStockAsync(line.ProductId, line.Quantity, ct);
                     if (newStock is null)
                     {
                            var name = catalog.First(p => p.Id == line.ProductId).Name;
                            return OrderPlacement.StockConflict($"Stock insuffisant pour « {name} ».");
                     }
                     updates.Add((line.ProductId, newStock.Value));
              }

              var store = await storeConfig.GetAsync(ct);
              var entity = new OrderEntity
              {
                     Id = Guid.CreateVersion7(),           // triable chronologiquement
                     IdempotencyKey = cmd.IdempotencyKey,
                     UserId = cmd.UserId,                   // sub Keycloak, déduit du jeton par l'API
                     Email = cmd.Email,
                     FullName = cmd.FullName,
                     Address = cmd.Address,
                     PostalCode = cmd.PostalCode,
                     Currency = store.Currency,
                     Status = OrderStatus.Pending,          // le paiement (phase 6) la fera passer à Paid
                     CreatedAt = DateTimeOffset.UtcNow,
                     TaxRate = store.VatRate,               // instantané : le taux peut changer plus tard
                     Lines = [.. cmd.Lines.Select(l =>
                     {
                            var p = catalog.First(c => c.Id == l.ProductId);
                            return new OrderLineEntity
                            {
                                   ProductId = p.Id,
                                   ProductName = p.Name,   // instantané au moment de l'achat
                                   UnitPrice = p.Price,
                                   Quantity = l.Quantity,
                                   LineTotal = p.Price * l.Quantity,
                            };
                     })],
              };
              // Code promo éventuel : validé puis CONSOMMÉ dans cette transaction —
              // l'UPDATE conditionnel re-vérifie tout côté base (filet anti-course),
              // et un rollback (stock insuffisant plus bas, doublon d'idempotence)
              // annule aussi la consommation. Le rejeu idempotent ne re-consomme pas.
              var subtotal = entity.Lines.Sum(l => l.LineTotal);
              if (!string.IsNullOrWhiteSpace(cmd.PromoCode))
              {
                     var promo = await promos.FindUsableAsync(cmd.PromoCode, ct);
                     if (promo is null || !await promos.TryConsumeAsync(promo.Code, ct))
                            return OrderPlacement.Invalid("Code promo invalide, expiré ou épuisé.");
                     entity.PromoCode = promo.Code;
                     entity.Discount = PromoDiscount.Compute(promo.Kind, promo.Value, subtotal);
              }

              // Livraison : règle unique StoreConfig (offerte dès le seuil), calculée
              // ICI — jamais côté navigateur — sur le sous-total APRÈS remise.
              // Total = marchandises − remise + livraison (TTC).
              entity.ShippingCost = store.ShippingFor(subtotal - entity.Discount);
              entity.Total = subtotal - entity.Discount + entity.ShippingCost;

              db.Orders.Add(entity);
              // Outbox : l'email de confirmation est garanti par la MÊME transaction
              // (le worker l'enverra de façon asynchrone — jamais perdu si le SMTP tousse).
              db.Outbox.Add(new OutboxMessageEntity
              {
                     Id = Guid.CreateVersion7(),
                     Type = OutboxTypes.OrderConfirmation,
                     OrderId = entity.Id,
                     CreatedAt = DateTimeOffset.UtcNow,
              });
              try
              {
                     await db.SaveChangesAsync(ct);
                     await tx.CommitAsync(ct);
              }
              catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                     { SqlState: PostgresErrorCodes.UniqueViolation })
              {
                     // Course : une requête portant la MÊME clé a validé entre-temps.
                     // On annule tout (décréments inclus) et on rejoue l'existante.
                     await tx.RollbackAsync(ct);
                     var replay = await FindByIdempotencyKeyAsync(cmd.IdempotencyKey, ct);
                     return replay is not null
                            ? OrderPlacement.Replayed(replay)
                            : OrderPlacement.Failed("Conflit d'idempotence irrésolu.");
              }

              return OrderPlacement.Created(entity.ToDomain(), updates);
       }

       public async Task<Order?> FindByIdAsync(Guid id, CancellationToken ct = default)
       {
              var entity = await db.Orders.AsNoTracking().Include(o => o.Lines)
                     .FirstOrDefaultAsync(o => o.Id == id, ct);
              return entity?.ToDomain();
       }

       /// <summary>Historique d'un client (pages « Mon compte ») : plus récentes d'abord.
       /// Guid v7 = tri chronologique par Id décroissant.</summary>
       public async Task<IReadOnlyList<Order>> GetForUserAsync(string userId, CancellationToken ct = default)
       {
              var entities = await db.Orders.AsNoTracking().Include(o => o.Lines)
                     .Where(o => o.UserId == userId)
                     .OrderByDescending(o => o.Id)
                     .ToListAsync(ct);
              return entities.Select(e => e.ToDomain()).ToList();
       }

       // ---- Administration des commandes (back-office) ----

       public async Task<PagedResult<Order>> GetOrdersAsync(OrderStatus? status = null,
              int skip = 0, int take = 25, CancellationToken ct = default)
       {
              var query = db.Orders.AsNoTracking().AsQueryable();
              if (status is { } s)
                     query = query.Where(o => o.Status == s);
              var total = await query.CountAsync(ct);
              var entities = await query.Include(o => o.Lines)
                     .OrderByDescending(o => o.Id)
                     .Skip(skip).Take(Math.Clamp(take, 1, 100))
                     .ToListAsync(ct);
              return new PagedResult<Order>(entities.Select(e => e.ToDomain()).ToList(), total);
       }

       /// <summary>Transitions autorisées : une commande ne recule jamais, une annulée
       /// ou expédiée est terminale. (Le remboursement/restock est une évolution.)</summary>
       private static readonly Dictionary<OrderStatus, OrderStatus[]> _transitions = new()
       {
              [OrderStatus.Pending] = [OrderStatus.Paid, OrderStatus.Cancelled],
              [OrderStatus.Paid] = [OrderStatus.Shipped, OrderStatus.Cancelled],
              [OrderStatus.Shipped] = [],
              [OrderStatus.Cancelled] = [],
       };

       public async Task<OrderWriteResult> SetStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct = default)
       {
              var entity = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId, ct);
              if (entity is null) return OrderWriteResult.NotFound();
              if (entity.Status == status) return OrderWriteResult.Ok(entity.ToDomain());   // idempotent
              if (!_transitions[entity.Status].Contains(status))
                     return OrderWriteResult.Invalid(
                            $"Transition interdite : {entity.Status} → {status}.");

              // Annulation = les marchandises retournent en rayon : restock atomique
              // dans la MÊME transaction que le changement de statut (tout ou rien).
              if (status == OrderStatus.Cancelled)
              {
                     await using var tx = await db.Database.BeginTransactionAsync(ct);
                     var updates = new List<(int, int)>();
                     foreach (var line in entity.Lines)
                     {
                            var newStock = await products.RestockAsync(line.ProductId, line.Quantity, ct);
                            if (newStock is { } stock)
                                   updates.Add((line.ProductId, stock));
                     }
                     entity.Status = status;
                     AddStatusOutbox(entity.Id);
                     await db.SaveChangesAsync(ct);
                     await tx.CommitAsync(ct);
                     return OrderWriteResult.Ok(entity.ToDomain(), updates);
              }

              entity.Status = status;
              AddStatusOutbox(entity.Id);
              await db.SaveChangesAsync(ct);
              return OrderWriteResult.Ok(entity.ToDomain());
       }

       private void AddStatusOutbox(Guid orderId) =>
              db.Outbox.Add(new OutboxMessageEntity
              {
                     Id = Guid.CreateVersion7(),
                     Type = OutboxTypes.OrderStatus,
                     OrderId = orderId,
                     CreatedAt = DateTimeOffset.UtcNow,
              });

       /// <summary>Mémorise la référence du paiement (Stripe pi_… / fake_…) sur la commande.</summary>
       public async Task SetPaymentIntentAsync(Guid orderId, string paymentIntentId, CancellationToken ct = default)
       {
              var entity = await db.Orders.FindAsync([orderId], ct);
              if (entity is not null && entity.PaymentIntentId != paymentIntentId)
              {
                     entity.PaymentIntentId = paymentIntentId;
                     await db.SaveChangesAsync(ct);
              }
       }

       private async Task<Order?> FindByIdempotencyKeyAsync(string key, CancellationToken ct)
       {
              var entity = await db.Orders.AsNoTracking().Include(o => o.Lines)
                     .FirstOrDefaultAsync(o => o.IdempotencyKey == key, ct);
              return entity?.ToDomain();
       }
}
