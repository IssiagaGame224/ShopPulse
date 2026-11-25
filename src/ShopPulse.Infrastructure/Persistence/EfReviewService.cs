using Microsoft.EntityFrameworkCore;
using ShopPulse.Domain;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Avis clients côté API : lecture publique (agrégat + avis récents),
/// soumission idempotente par (produit, utilisateur) — une nouvelle soumission
/// REMPLACE l'avis existant — et modération back-office.</summary>
public class EfReviewService(ShopPulseDbContext db)
{
       private const int MaxReviewsPerProduct = 50;

       /// <summary>Agrégat + avis récents d'un produit. Null si le produit n'existe pas
       /// (404 côté API — même convention que le reste du catalogue).</summary>
       public async Task<ProductReviews?> GetForProductAsync(int productId, CancellationToken ct = default)
       {
              if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
                     return null;

              var reviews = await db.Reviews.AsNoTracking()
                     .Where(r => r.ProductId == productId)
                     .OrderByDescending(r => r.CreatedAt)
                     .Take(MaxReviewsPerProduct)
                     .ToListAsync(ct);

              var rating = reviews.Count == 0
                     ? new ProductRating(0, 0)
                     : new ProductRating(Math.Round(reviews.Average(r => r.Rating), 1), reviews.Count);
              return new ProductReviews(rating, [.. reviews.Select(r => r.ToDomain())]);
       }

       /// <summary>Crée ou remplace l'avis de CE client sur CE produit (la contrainte
       /// d'unicité en base double la règle applicative). Null si le produit n'existe pas.</summary>
       public async Task<Review?> UpsertAsync(
              int productId, string userId, string authorName, int rating, string comment,
              CancellationToken ct = default)
       {
              if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
                     return null;

              var entity = await db.Reviews
                     .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId, ct);
              if (entity is null)
              {
                     entity = new ReviewEntity
                     {
                            ProductId = productId, UserId = userId, AuthorName = authorName,
                            Rating = rating, Comment = comment, CreatedAt = DateTimeOffset.UtcNow,
                     };
                     db.Reviews.Add(entity);
              }
              else
              {
                     entity.Rating = rating;
                     entity.Comment = comment;
                     entity.AuthorName = authorName;   // le nom d'affichage a pu changer
              }
              await db.SaveChangesAsync(ct);
              return entity.ToDomain();
       }

       /// <summary>Modération : les avis les plus récents, tous produits confondus.</summary>
       public async Task<IReadOnlyList<AdminReview>> GetRecentAsync(int take = 100, CancellationToken ct = default) =>
              await db.Reviews.AsNoTracking()
                     .OrderByDescending(r => r.CreatedAt)
                     .Take(take)
                     .Select(r => new AdminReview(r.Id, r.ProductId, r.Product!.Name,
                            r.AuthorName, r.Rating, r.Comment, r.CreatedAt))
                     .ToListAsync(ct);

       public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
       {
              var entity = await db.Reviews.FindAsync([id], ct);
              if (entity is null) return false;
              db.Reviews.Remove(entity);
              await db.SaveChangesAsync(ct);
              return true;
       }
}
