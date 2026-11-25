using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Catégorie en base : fini les chaînes libres et la liste codée en dur dans l'UI.</summary>
public class CategoryEntity
{
       public int Id { get; set; }
       public required string Name { get; set; }
}

public class CategoryEntityConfiguration : IEntityTypeConfiguration<CategoryEntity>
{
       public void Configure(EntityTypeBuilder<CategoryEntity> c)
       {
              c.ToTable("categories");
              c.HasKey(x => x.Id);
              c.Property(x => x.Id).ValueGeneratedNever();   // ids du seed (CatalogSeed.Catégories)
              c.Property(x => x.Name).HasMaxLength(100).IsRequired();
              c.HasIndex(x => x.Name).IsUnique();
              c.HasData(CatalogSeed.Catégories.Select(m =>
                     new CategoryEntity { Id = m.Id, Name = m.Name }));
       }
}
