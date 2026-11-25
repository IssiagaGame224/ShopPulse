using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Permet à `dotnet ef` (migrations) de s'exécuter depuis le seul projet
/// Infrastructure, sans démarrer l'API : bonne pratique pour un DbContext en classlib.
/// La chaîne de connexion ne sert qu'au design-time (scaffold) ; à l'exécution,
/// l'API fournit la sienne via la configuration.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ShopPulseDbContext>
{
       public ShopPulseDbContext CreateDbContext(string[] args) =>
              new(new DbContextOptionsBuilder<ShopPulseDbContext>()
                     .UseNpgsql("Host=localhost;Port=5432;Database=shoppulse;Username=shoppulse;Password=dev-password-shoppulse")
                     .Options);
}
