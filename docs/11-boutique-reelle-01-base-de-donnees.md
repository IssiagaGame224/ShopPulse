# 11 — Vraie boutique, phase 1 : la base de données

> **Contexte.** Le cours est terminé (modules 0 à 8 + bonus déploiement). ShopPulse devient
> maintenant une **vraie boutique** : base de données réelle, comptes clients, commandes,
> back-office et paiement. Ce chapitre couvre la **phase 1** : remplacer les données en
> mémoire par **PostgreSQL + Entity Framework Core**, sans toucher à l'interface.

## Ce qui change (et ce qui ne change pas)

L'architecture modulaire du module 4 paie ses dividendes : l'interface `IProductService`
(module 6) isole les consommateurs de l'implémentation. On branche donc une nouvelle
implémentation **derrière la même interface** :

| Avant | Après |
|---|---|
| `InMemoryProductService` (singleton, données perdues au redémarrage) | `EfProductService` (scoped, PostgreSQL) |
| `SetStock` en mémoire, non thread-safe | `SetStockAsync` persistant (`SaveChangesAsync`) |
| Catalogue régénéré à chaque démarrage | Seed **idempotent** via les migrations EF |

Le front Blazor, l'API HTTP, le hub SignalR et les tests d'architecture : **inchangés**.

## PostgreSQL + Npgsql + EF Core

Trois paquets, versions **alignées sur le runtime .NET 10** (règle du projet) :

- `Npgsql.EntityFrameworkCore.PostgreSQL` **10.0.4** — le provider PostgreSQL ;
- `Microsoft.EntityFrameworkCore.Design` **10.0.15** — outils de migration, en
  `PrivateAssets=all` (jamais embarqué dans la publication) ;
- `dotnet-ef` mis à jour en outil global (`dotnet tool update -g dotnet-ef`).

**Piège.** Les versions 11.x de ces paquets existent déjà : un `dotnet add package` sans
version les aurait résolues et désalignées du runtime. On épingle la majeure **10** dans
`Directory.Packages.props`, comme les `Microsoft.AspNetCore.*`.

## L'entité de persistance n'est pas le modèle Domain

Le record `Product` du Domain reste **pur** : la règle NetArchTest interdit au Domain de
référencer `Microsoft.EntityFrameworkCore`. On crée donc dans
`Infrastructure/Persistence/` :

- **`ProductEntity`** — la forme « base de données », avec ses conversions
  `ToDomain()` / `FromDomain()` ;
- **`ProductEntityConfiguration`** (`IEntityTypeConfiguration`) — le schéma :
  table `products`, longueurs maximales, index sur `Category`, et surtout
  **`Price` en `decimal(18,2)`** : jamais de `float`/`double` pour l'argent
  (les arrondis binaires font perdre des centimes) ;
- **`ShopPulseDbContext`** — le contexte, qui applique les configurations de l'assembly.

**Boutique multi-pays.** Le prix est stocké en décimal *sans* devise figée dans le schéma
à ce stade ; la devise et la TVA par pays arriveront avec les commandes (phase 3). Le
formatage reste l'affaire de la `Culture` côté UI (module 8), jamais de la base.

## Le seed : une seule source de vérité

Les 10 modèles réels et la génération des 200 produits de démonstration sont extraits de
`InMemoryProductService` vers **`CatalogSeed`** (`Infrastructure/Persistence/`), partagé par :

1. `InMemoryProductService` (conservé pour les tests et le dev hors base) ;
2. le **seeding EF** : `HasData(...)` dans la configuration, donc *dans la migration elle-même*.

**Piège.** `HasData` exige des valeurs **déterministes** : toute génération aléatoire ou
dépendante de l'horloge produirait une migration différente à chaque exécution. La
génération actuelle (formules sur l'index) est stable, elle convient.

## La migration initiale

```bash
dotnet ef migrations add InitialCreate \
  -p src/ShopPulse.Infrastructure -s src/ShopPulse.Api -o Persistence/Migrations
```

**Piège.** Le projet de *démarrage* (`-s`, ici l'API) doit lui aussi référencer
`Microsoft.EntityFrameworkCore.Design`, sinon les outils refusent de s'exécuter.

Les migrations sont **appliquées au démarrage de l'API** (`db.Database.Migrate()`,
idempotent) : le schéma et le seed se créent seuls au premier lancement, y compris dans
les conteneurs.

**Limite.** Acceptable pour une instance unique. En production multi-instances, deux
répliques qui migrent en même temps se concurrencent : on préférera un *migration bundle*
ou un job dédié.

## `EfProductService` : les bonnes pratiques de lecture

- **`AsNoTracking()`** sur toutes les lectures : pas de suivi de changements à payer pour
  de la lecture seule ;
- **recherche `ILIKE`** traduite en SQL (`EF.Functions.ILike`), insensible à la casse,
  avec **échappement** des jokers `%`, `_` et `\` saisis par l'utilisateur ;
- **pagination côté serveur** : `CountAsync` + `Skip/Take`, jamais de chargement complet
  en mémoire ;
- `SetStockAsync` retourne `bool` → l'endpoint `/api/stock` répond **404** sur un id inconnu.

**Limite.** La concurrence optimiste sur le stock (deux commandes simultanées) n'est pas
encore traitée : elle viendra avec la phase Commandes (décrément transactionnel).

## Le cache reste, l'invalidation devient réelle

Le décorateur `CachedProductService` (module 5) est conservé devant EF, mais l'écriture
invalide désormais le tag `"catalog"` de `HybridCache` dans l'endpoint `/api/stock` —
la règle d'or « tout cache est une promesse d'invalidation » devient effective, alors
qu'avec l'in-memory le cache et la donnée vivaient et mouraient ensemble.

## Docker Compose : le service `db`

```yaml
db:
  image: postgres:17-alpine
  environment:
    POSTGRES_DB: shoppulse
    POSTGRES_USER: shoppulse
    POSTGRES_PASSWORD: dev-password-shoppulse   # dev uniquement
  volumes: [ pgdata:/var/lib/postgresql/data ]  # données persistantes
  ports: [ "5432:5432" ]                        # pour `dotnet run` hors conteneur
  healthcheck: { test: ["CMD-SHELL", "pg_isready -U shoppulse -d shoppulse"], ... }
```

L'API attend que la base soit **saine** (`depends_on: condition: service_healthy`) et
reçoit sa chaîne de connexion par `ConnectionStrings__ShopPulse`. En local, la même clé
est dans `appsettings.Development.json` (pointe sur `localhost`).

**Piège.** Le port 5432 publié peut entrer en conflit avec un PostgreSQL déjà installé sur
la machine : changer le mapping (`"5433:5432"`) et la chaîne de connexion locale en
conséquence.

## Vérifications effectuées

- `dotnet build ShopPulse.slnx` : 0 avertissement, 0 erreur (`TreatWarningsAsErrors`) ;
- `dotnet test tests/ShopPulse.Architecture.Tests` : **14/14** (le Domain reste pur) ;
- bout en bout : `docker compose up -d db` + `dotnet run` (API) →
  `GET /api/products` (200, données PostgreSQL), `GET /api/products/42` (200),
  `GET /api/products/9999` (404), `POST /api/stock` (204 → stock relu à jour, cache
  invalidé ; 404 sur id inconnu) ; `psql` confirme 200 lignes seedées.

## Bilan des notions

- EF Core derrière une interface : l'UI et l'API ne savent pas que la base a changé ;
- entité de persistance ≠ modèle Domain (frontières imposées par NetArchTest) ;
- `decimal(18,2)` pour l'argent, `AsNoTracking` en lecture, `ILIKE` échappé, pagination SQL ;
- migrations + `HasData` déterministe, appliquées au démarrage ;
- cache décorateur conservé, invalidation par tag à l'écriture ;
- secret de connexion : configuration par environnement, jamais dans le dépôt.

**Prochaine phase** : catalogue complet (catégories en base, filtres, stock transactionnel).
