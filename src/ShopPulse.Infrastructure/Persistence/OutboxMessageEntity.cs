using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Pattern Outbox : l'intention d'envoi d'email est écrite dans la MÊME
/// transaction que l'événement métier (commande, changement de statut). Un worker
/// la traite ensuite de façon asynchrone : un crash ou un SMTP en panne ne perd
/// plus jamais un email — la ligne attend d'être rejouée.</summary>
public class OutboxMessageEntity
{
       public Guid Id { get; set; }                    // Guid v7 : ordre chronologique
       public required string Type { get; set; }       // OrderConfirmation | OrderStatus
       public Guid OrderId { get; set; }
       public DateTimeOffset CreatedAt { get; set; }
       public DateTimeOffset? ProcessedAt { get; set; }
       public int Attempts { get; set; }
       public string? LastError { get; set; }
}

public static class OutboxTypes
{
       public const string OrderConfirmation = "OrderConfirmation";
       public const string OrderStatus = "OrderStatus";
}

public class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
       public void Configure(EntityTypeBuilder<OutboxMessageEntity> o)
       {
              o.ToTable("outbox_messages");
              o.HasKey(x => x.Id);
              o.Property(x => x.Id).ValueGeneratedNever();
              o.Property(x => x.Type).HasMaxLength(40).IsRequired();
              o.Property(x => x.LastError).HasMaxLength(1000);
              // Le worker ne lit que les messages en attente, dans l'ordre.
              o.HasIndex(x => new { x.ProcessedAt, x.Id });
       }
}
