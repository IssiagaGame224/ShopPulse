using Microsoft.EntityFrameworkCore;

namespace ShopPulse.Infrastructure.Persistence;

public class ShopPulseDbContext(DbContextOptions<ShopPulseDbContext> options) : DbContext(options)
{
       public DbSet<ProductEntity> Products => Set<ProductEntity>();
       public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
       public DbSet<OrderEntity> Orders => Set<OrderEntity>();
       public DbSet<OutboxMessageEntity> Outbox => Set<OutboxMessageEntity>();

       protected override void OnModelCreating(ModelBuilder modelBuilder) =>
              modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShopPulseDbContext).Assembly);
}
