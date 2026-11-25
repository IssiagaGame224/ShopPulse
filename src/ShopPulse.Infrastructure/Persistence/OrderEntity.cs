using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPulse.Domain;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Commande persistée. L'adresse, l'email et le total sont des instantanés :
/// une commande historique ne change jamais, même si le client ou le catalogue évolue.
/// UserId (sub Keycloak) reste nul tant que les comptes clients (phase 4) ne sont pas là.</summary>
public class OrderEntity
{
       public Guid Id { get; set; }
       public required string IdempotencyKey { get; set; }
       public string? UserId { get; set; }
       public required string Email { get; set; }
       public required string FullName { get; set; }
       public required string Address { get; set; }
       public required string PostalCode { get; set; }
       public required string Currency { get; set; }
       public OrderStatus Status { get; set; }
       public DateTimeOffset CreatedAt { get; set; }
       public decimal Total { get; set; }
       public string? PaymentIntentId { get; set; }
       public List<OrderLineEntity> Lines { get; set; } = [];

       public Order ToDomain() =>
              new(Id, Email, FullName, Address, PostalCode, Currency, Status, CreatedAt,
                     Lines.Select(l => l.ToDomain()).ToList(), UserId, PaymentIntentId);
}

/// <summary>Ligne persistée : ProductId est une référence SOUPLE (pas de clé étrangère
/// vers products) — le catalogue peut supprimer un produit sans toucher l'historique ;
/// nom et prix unitaire sont copiés au moment de l'achat.</summary>
public class OrderLineEntity
{
       public int Id { get; set; }
       public Guid OrderId { get; set; }
       public int ProductId { get; set; }
       public required string ProductName { get; set; }
       public decimal UnitPrice { get; set; }
       public int Quantity { get; set; }
       public decimal LineTotal { get; set; }

       public OrderLine ToDomain() => new(ProductId, ProductName, UnitPrice, Quantity);
}

public class OrderEntityConfiguration : IEntityTypeConfiguration<OrderEntity>
{
       public void Configure(EntityTypeBuilder<OrderEntity> o)
       {
              o.ToTable("orders");
              o.HasKey(x => x.Id);
              o.Property(x => x.Id).ValueGeneratedNever();   // Guid v7 généré côté application
              o.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
              // La clé d'idempotence est UNIQUE : la base elle-même interdit le doublon,
              // même sous insertion concurrente (le filet de sécurité final).
              o.HasIndex(x => x.IdempotencyKey).IsUnique();
              o.Property(x => x.UserId).HasMaxLength(100);
              o.Property(x => x.Email).HasMaxLength(256).IsRequired();
              o.Property(x => x.FullName).HasMaxLength(80).IsRequired();
              o.Property(x => x.Address).HasMaxLength(200).IsRequired();
              o.Property(x => x.PostalCode).HasMaxLength(10).IsRequired();
              o.Property(x => x.Currency).HasMaxLength(3).IsRequired();
              // Statut en clair dans la base ("Pending", "Paid"...) : lisible en SQL.
              o.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
              o.Property(x => x.Total).HasPrecision(18, 2);
              o.Property(x => x.PaymentIntentId).HasMaxLength(100);
              o.HasMany(x => x.Lines).WithOne()
                     .HasForeignKey(l => l.OrderId)
                     .OnDelete(DeleteBehavior.Cascade);       // les lignes meurent avec leur commande
       }
}

public class OrderLineEntityConfiguration : IEntityTypeConfiguration<OrderLineEntity>
{
       public void Configure(EntityTypeBuilder<OrderLineEntity> l)
       {
              l.ToTable("order_lines");
              l.HasKey(x => x.Id);
              l.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
              l.Property(x => x.UnitPrice).HasPrecision(18, 2);
              l.Property(x => x.LineTotal).HasPrecision(18, 2);
              l.HasIndex(x => x.ProductId);
       }
}
