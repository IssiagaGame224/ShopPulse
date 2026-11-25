using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPulse.Domain;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Entité de persistance d'un avis client (table reviews). UserId = sub Keycloak :
/// la contrainte d'unicité (ProductId, UserId) garantit UN avis par client et par produit —
/// la règle est dans la base, pas seulement dans le code.</summary>
public class ReviewEntity
{
       public int Id { get; set; }
       public int ProductId { get; set; }
       public required string UserId { get; set; }
       public required string AuthorName { get; set; }
       public int Rating { get; set; }
       public required string Comment { get; set; }
       public DateTimeOffset CreatedAt { get; set; }
       public ProductEntity Product { get; set; } = null!;

       public Review ToDomain() =>
              new(Id, ProductId, AuthorName, Rating, Comment, CreatedAt);
}

public class ReviewEntityConfiguration : IEntityTypeConfiguration<ReviewEntity>
{
       public void Configure(EntityTypeBuilder<ReviewEntity> r)
       {
              r.ToTable("reviews", t => t.HasCheckConstraint("ck_reviews_rating", "\"Rating\" BETWEEN 1 AND 5"));
              r.HasKey(x => x.Id);
              r.Property(x => x.Id).UseIdentityColumn();
              r.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                     .OnDelete(DeleteBehavior.Restrict);      // comme le catalogue : jamais de cascade
              r.HasIndex(x => new { x.ProductId, x.UserId }).IsUnique();   // un avis par client et par produit
              r.Property(x => x.UserId).HasMaxLength(64).IsRequired();
              r.Property(x => x.AuthorName).HasMaxLength(100).IsRequired();
              r.Property(x => x.Comment).HasMaxLength(1000).IsRequired();
       }
}
