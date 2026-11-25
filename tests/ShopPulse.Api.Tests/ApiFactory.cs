using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace ShopPulse.Api.Tests;

/// <summary>Hôte de test : l'API réelle (WebApplicationFactory) contre un PostgreSQL
/// éphémère (Testcontainers — migrations + seed appliqués au démarrage, comme en prod).
/// Keycloak : l'instance de dev (localhost:8080) fournit les jetons, comme pour les E2E.
/// Prérequis : Docker + `docker compose up -d keycloak`.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
       private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
              .Build();

       protected override void ConfigureWebHost(IWebHostBuilder builder)
       {
              builder.UseEnvironment("Development");
              builder.ConfigureAppConfiguration((_, config) =>
                     config.AddInMemoryCollection(new Dictionary<string, string?>
                     {
                            ["ConnectionStrings:ShopPulse"] = _postgres.GetConnectionString(),
                     }));
       }

       public async Task InitializeAsync() => await _postgres.StartAsync();

       async Task IAsyncLifetime.DisposeAsync()
       {
              await _postgres.DisposeAsync();
              await base.DisposeAsync();
       }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
