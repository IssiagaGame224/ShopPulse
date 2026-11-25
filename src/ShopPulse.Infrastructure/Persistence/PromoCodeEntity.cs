using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPulse.Domain;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Entité de persistance d'un code promo (table promo_codes). Le code est
/// stocké en MAJUSCULES (comparaison insensible à la casse garantie par normalisation
/// à l'écriture et à la lecture). Deux codes de démonstration sont semés.</summary>
public class PromoCodeEntity
{
       public int Id { get; set; }
       public required string Code { get; set; }
       public PromoKind Kind { get; set; }
       public decimal Value { get; set; }
       public DateTimeOffset? ExpiresAt { get; set; }
       public int? MaxUses { get; set; }
       public int UsedCount { get; set; }
       public bool Active { get; set; }

       public PromoCode ToDomain() =>
              new(Id, Code, Kind, Value, ExpiresAt, MaxUses, UsedCount, Active);
}

public class PromoCodeEntityConfiguration : IEntityTypeConfiguration<PromoCodeEntity>
{
       public void Configure(EntityTypeBuilder<PromoCodeEntity> p)
       {
              p.ToTable("promo_codes", t => t.HasCheckConstraint(
                     "ck_promo_codes_value", "\"Kind\" <> 'Percent' OR (\"Value\" > 0 AND \"Value\" <= 100)"));
              p.HasKey(x => x.Id);
              p.Property(x => x.Id).UseIdentityColumn();
              p.Property(x => x.Code).HasMaxLength(32).IsRequired();
              p.HasIndex(x => x.Code).IsUnique();
              p.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
              p.Property(x => x.Value).HasPrecision(18, 2);
              // Codes de démonstration (dev/E2E) : -10 % et -5 € fixes.
              p.HasData(
                     new PromoCodeEntity { Id = 1, Code = "BIENVENUE10", Kind = PromoKind.Percent,
                            Value = 10m, Active = true },
                     new PromoCodeEntity { Id = 2, Code = "FIXE5", Kind = PromoKind.Amount,
                            Value = 5m, Active = true });
       }
}
