using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShopPulse.Application;
using ShopPulse.Domain;
using System.Data;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Implémentation EF Core / PostgreSQL de <see cref="IProductService"/>.
/// Lectures en AsNoTracking (pas de suivi de changements pour de la lecture seule),
/// recherche insensible à la casse traduite en SQL (ILIKE), filtres et pagination
/// côté serveur : jamais de chargement complet de la table en mémoire.</summary>
public class EfProductService(ShopPulseDbContext db) : IProductService, IProductAdmin
{
       public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
       {
              var entities = await db.Products.AsNoTracking().Include(p => p.Category)
                     .OrderBy(p => p.Id).ToListAsync(ct);
              return entities.Select(e => e.ToDomain()).ToList();
       }

       public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
       {
              var entity = await db.Products.AsNoTracking().Include(p => p.Category)
                     .FirstOrDefaultAsync(p => p.Id == id, ct);
              return entity?.ToDomain();
       }

       public async Task<PagedResult<Product>> GetPageAsync(
              int skip, int take, string? search, string? category = null, CancellationToken ct = default)
       {
              var query = db.Products.AsNoTracking().AsQueryable();
              if (!string.IsNullOrWhiteSpace(category))
                     query = query.Where(p => p.Category.Name == category);
              if (!string.IsNullOrWhiteSpace(search))
              {
                     var pattern = $"%{search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                     query = query.Where(p => EF.Functions.ILike(p.Name, pattern, "\\"));
              }

              var total = await query.CountAsync(ct);
              var entities = await query.Include(p => p.Category)
                     .OrderBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);
              return new PagedResult<Product>(entities.Select(e => e.ToDomain()).ToList(), total);
       }

       public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default) =>
              await db.Categories.AsNoTracking().OrderBy(c => c.Name)
                     .Select(c => c.Name).ToListAsync(ct);

       /// <summary>Écriture de stock persistante (back-office).</summary>
       public async Task<bool> SetStockAsync(int id, int stock, CancellationToken ct = default)
       {
              var entity = await db.Products.FindAsync([id], ct);
              if (entity is null) return false;
              entity.Stock = stock;
              await db.SaveChangesAsync(ct);
              return true;
       }

       /// <summary>Décrément de stock ATOMIQUE : un seul UPDATE conditionnel
       /// (WHERE "Stock" &gt;= quantité) — deux commandes simultanées ne peuvent pas
       /// passer sous zéro, sans verrou applicatif ni retry. RETURNING donne le nouveau
       /// stock dans le même aller-retour (pour le hub SignalR).
       /// Exécuté en ADO.NET sur la connexion du contexte : EF Core ne sait pas composer
       /// du LINQ sur un UPDATE...RETURNING.
       /// Résultat : null si l'UPDATE n'a touché aucune ligne (produit inconnu OU stock
       /// insuffisant — l'appelant distingue via <see cref="ExistsAsync"/>).</summary>
       public async Task<int?> TryDecrementStockAsync(int id, int quantity, CancellationToken ct = default)
       {
              if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
              var conn = db.Database.GetDbConnection();
              var wasOpen = conn.State == ConnectionState.Open;
              if (!wasOpen) await conn.OpenAsync(ct);
              try
              {
                     await using var cmd = conn.CreateCommand();
                     // Enrôlement explicite dans la transaction du contexte si elle existe
                     // (création de commande) : le décrément suit le sort de la transaction.
                     cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                     cmd.CommandText = """
                            UPDATE products SET "Stock" = "Stock" - @quantity
                            WHERE "Id" = @id AND "Stock" >= @quantity
                            RETURNING "Stock";
                            """;
                     var pId = cmd.CreateParameter(); pId.ParameterName = "id"; pId.Value = id;
                     var pQty = cmd.CreateParameter(); pQty.ParameterName = "quantity"; pQty.Value = quantity;
                     cmd.Parameters.Add(pId); cmd.Parameters.Add(pQty);
                     var result = await cmd.ExecuteScalarAsync(ct);
                     return result is null or DBNull ? null : Convert.ToInt32(result);
              }
              finally
              {
                     if (!wasOpen) await conn.CloseAsync();
              }
       }

       public Task<bool> ExistsAsync(int id, CancellationToken ct = default) =>
              db.Products.AnyAsync(p => p.Id == id, ct);

       /// <summary>Ré-incrément atomique du stock (annulation de commande) : même technique
       /// que le décrément — un seul UPDATE, RETURNING pour le temps réel. Null si le
       /// produit n'existe plus (supprimé du catalogue entre-temps : on ignore).</summary>
       public async Task<int?> RestockAsync(int id, int quantity, CancellationToken ct = default)
       {
              if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
              var conn = db.Database.GetDbConnection();
              var wasOpen = conn.State == ConnectionState.Open;
              if (!wasOpen) await conn.OpenAsync(ct);
              try
              {
                     await using var cmd = conn.CreateCommand();
                     cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                     cmd.CommandText = """
                            UPDATE products SET "Stock" = "Stock" + @quantity
                            WHERE "Id" = @id
                            RETURNING "Stock";
                            """;
                     var pId = cmd.CreateParameter(); pId.ParameterName = "id"; pId.Value = id;
                     var pQty = cmd.CreateParameter(); pQty.ParameterName = "quantity"; pQty.Value = quantity;
                     cmd.Parameters.Add(pId); cmd.Parameters.Add(pQty);
                     var result = await cmd.ExecuteScalarAsync(ct);
                     return result is null or DBNull ? null : Convert.ToInt32(result);
              }
              finally
              {
                     if (!wasOpen) await conn.CloseAsync();
              }
       }

       // ---- Administration du catalogue (back-office) ----

       public async Task<ProductWriteResult> CreateAsync(ProductUpsert upsert, CancellationToken ct = default)
       {
              if (await ValidateAsync(upsert, excludeId: null, ct) is { } error)
                     return ProductWriteResult.Invalid(error);

              var entity = new ProductEntity
              {
                     Name = upsert.Name, CategoryId = await CategoryIdOfAsync(upsert.Category, ct),
                     Price = upsert.Price, OldPrice = upsert.OldPrice, Stock = upsert.Stock,
                     ImageUrl = upsert.ImageUrl, Description = upsert.Description,
                     Sku = upsert.Sku, Features = [.. upsert.Features],
              };
              db.Products.Add(entity);
              await db.SaveChangesAsync(ct);   // l'id est généré par la base (identity)
              return ProductWriteResult.Ok((await GetByIdAsync(entity.Id, ct))!);
       }

       public async Task<ProductWriteResult> UpdateAsync(int id, ProductUpsert upsert, CancellationToken ct = default)
       {
              var entity = await db.Products.FindAsync([id], ct);
              if (entity is null) return ProductWriteResult.Invalid($"Produit {id} introuvable.");
              if (await ValidateAsync(upsert, excludeId: id, ct) is { } error)
                     return ProductWriteResult.Invalid(error);

              entity.Name = upsert.Name;
              entity.CategoryId = await CategoryIdOfAsync(upsert.Category, ct);
              entity.Price = upsert.Price;
              entity.OldPrice = upsert.OldPrice;
              entity.Stock = upsert.Stock;
              entity.ImageUrl = upsert.ImageUrl;
              entity.Description = upsert.Description;
              entity.Sku = upsert.Sku;
              entity.Features = [.. upsert.Features];
              await db.SaveChangesAsync(ct);
              return ProductWriteResult.Ok((await GetByIdAsync(id, ct))!);
       }

       /// <summary>Validation serveur partagée création/mise à jour (jamais de confiance
       /// dans le formulaire). Retourne le message d'erreur, ou null si tout est valide.</summary>
       private async Task<string?> ValidateAsync(ProductUpsert u, int? excludeId, CancellationToken ct)
       {
              if (string.IsNullOrWhiteSpace(u.Sku)) return "La référence (SKU) est obligatoire.";
              if (string.IsNullOrWhiteSpace(u.Name)) return "Le nom est obligatoire.";
              if (u.Price <= 0) return "Le prix doit être positif.";
              if (u.Stock < 0) return "Le stock ne peut pas être négatif.";
              if (u.OldPrice is { } old && old <= u.Price)
                     return "Le prix barré doit être supérieur au prix (sinon, le laisser vide).";
              if (!await db.Categories.AnyAsync(c => c.Name == u.Category, ct))
                     return $"Catégorie inconnue : « {u.Category} ».";
              if (await db.Products.AnyAsync(p => p.Sku == u.Sku && p.Id != excludeId, ct))
                     return $"La référence « {u.Sku} » est déjà utilisée.";
              return null;
       }

       private async Task<int> CategoryIdOfAsync(string name, CancellationToken ct) =>
              await db.Categories.Where(c => c.Name == name).Select(c => c.Id).FirstAsync(ct);
}
