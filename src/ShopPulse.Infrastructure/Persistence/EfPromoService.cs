using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShopPulse.Domain;
using System.Data;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Codes promo côté API. Trois usages :
/// — validation SANS consommation (aperçu panier) ;
/// — consommation atomique dans la transaction de la commande (UPDATE conditionnel :
///   deux commandes simultanées ne peuvent pas dépasser MaxUses, comme le stock) ;
/// — administration (création, activation/désactivation).</summary>
public class EfPromoService(ShopPulseDbContext db)
{
       public static string Normalize(string code) => code.Trim().ToUpperInvariant();

       /// <summary>Le code utilisable maintenant, ou null (inconnu, inactif, expiré, épuisé).</summary>
       public async Task<PromoCode?> FindUsableAsync(string code, CancellationToken ct = default)
       {
              var normalized = Normalize(code);
              var entity = await db.PromoCodes.AsNoTracking()
                     .FirstOrDefaultAsync(p => p.Code == normalized, ct);
              return entity is not null && entity.ToDomain().IsUsable(DateTimeOffset.UtcNow)
                     ? entity.ToDomain()
                     : null;
       }

       /// <summary>Consommation atomique, enrôlée dans la transaction du contexte si elle
       /// existe (création de commande) : la base elle-même re-vérifie toutes les conditions —
       /// le filet final contre la course « deux commandes, une seule utilisation restante ».</summary>
       public async Task<bool> TryConsumeAsync(string normalizedCode, CancellationToken ct = default)
       {
              var conn = db.Database.GetDbConnection();
              var wasOpen = conn.State == ConnectionState.Open;
              if (!wasOpen) await conn.OpenAsync(ct);
              try
              {
                     await using var cmd = conn.CreateCommand();
                     cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                     cmd.CommandText = """
                            UPDATE promo_codes SET "UsedCount" = "UsedCount" + 1
                            WHERE "Code" = @code AND "Active"
                              AND ("MaxUses" IS NULL OR "UsedCount" < "MaxUses")
                              AND ("ExpiresAt" IS NULL OR "ExpiresAt" > now());
                            """;
                     var p = cmd.CreateParameter(); p.ParameterName = "code"; p.Value = normalizedCode;
                     cmd.Parameters.Add(p);
                     return await cmd.ExecuteNonQueryAsync(ct) > 0;
              }
              finally
              {
                     if (!wasOpen) await conn.CloseAsync();
              }
       }

       // ---- Administration ----

       public async Task<IReadOnlyList<PromoCode>> GetAllAsync(CancellationToken ct = default) =>
              [.. (await db.PromoCodes.AsNoTracking().OrderBy(p => p.Code).ToListAsync(ct))
                     .Select(p => p.ToDomain())];

       /// <summary>Création : code normalisé en majuscules, unique ; pourcentage dans 1-100,
       /// montant positif. Retourne (promo, null) ou (null, message d'erreur).</summary>
       public async Task<(PromoCode? Promo, string? Error)> CreateAsync(
              string code, PromoKind kind, decimal value, DateTimeOffset? expiresAt, int? maxUses,
              CancellationToken ct = default)
       {
              var normalized = Normalize(code);
              if (normalized.Length is 0 or > 32)
                     return (null, "Le code est obligatoire (32 caractères maximum).");
              if (kind == PromoKind.Percent && value is <= 0 or > 100)
                     return (null, "Un pourcentage doit être compris entre 1 et 100.");
              if (kind == PromoKind.Amount && value <= 0)
                     return (null, "Le montant doit être positif.");
              if (maxUses is <= 0)
                     return (null, "Le nombre maximal d'utilisations doit être positif.");
              if (await db.PromoCodes.AnyAsync(p => p.Code == normalized, ct))
                     return (null, $"Le code « {normalized} » existe déjà.");

              var entity = new PromoCodeEntity
              {
                     Code = normalized, Kind = kind, Value = value,
                     ExpiresAt = expiresAt, MaxUses = maxUses, Active = true,
              };
              db.PromoCodes.Add(entity);
              await db.SaveChangesAsync(ct);
              return (entity.ToDomain(), null);
       }

       public async Task<bool> SetActiveAsync(int id, bool active, CancellationToken ct = default)
       {
              var entity = await db.PromoCodes.FindAsync([id], ct);
              if (entity is null) return false;
              entity.Active = active;
              await db.SaveChangesAsync(ct);
              return true;
       }
}
