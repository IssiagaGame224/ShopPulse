# Chapitre 6 — Module 6 : Networking (le réseau)

> **Question du module :** comment l'interface parle-t-elle aux données, de façon rapide, fiable et économe ?
> **Avec Blazor Server, il y a DEUX réseaux** : le circuit SignalR (navigateur ↔ serveur) et les appels HTTP sortants (serveur ↔ API). Ce module traite surtout du second.
> **Fil rouge :** extraction d'une vraie API (`ShopPulse.Api`), HttpClient typé et résilient, stock temps réel par SignalR, file d'attente hors ligne avec idempotence.

## Évolution majeure : une API séparée

Jusqu'ici, `IProductService` lisait des données en mémoire. Le module extrait une API et un projet de contrats :

```
src/
  ShopPulse.Contracts/    # DTO partagés (records) référencés par l'API ET le client
  ShopPulse.Api/          # Minimal API : /api/products, /api/stock, /hubs/stock — port 5299
  ShopPulse.Web/          # Blazor Server : appelle l'API via HttpClient typé — port 5026
```

```csharp
// ShopPulse.Contracts
public sealed record ProductDto(int Id, string Name, string Category, decimal Price, int Stock, ...);
public sealed record PageDto<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record ProblemInfo(string Title, int Status, string? Detail);
```

```csharp
// ShopPulse.Infrastructure/ProductApiClient.cs : IProductService devient un client HTTP
public sealed class ProductApiClient(HttpClient http) : IProductService
{
       public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
              => await http.GetFromJsonAsync<List<Product>>("api/products", ct) ?? [];
}
```

```csharp
// ShopPulse.Web/Program.cs
builder.Services.AddHttpClient<IProductService, ProductApiClient>(c =>
              c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]!));
```

**Vérifié bout en bout** : un POST `/api/stock` (produit 1 → 7) se reflète dans la page Web (« 7 en stock ») sans toucher au Web. `AddHttpClient` gère le pool de connexions et la rotation DNS : **ne faites jamais `new HttpClient()` par appel** — l'une des fuites de ressources les plus classiques de .NET.

## 1. REST vs GraphQL

| | REST | GraphQL |
|---|---|---|
| Sur/sous-récupération | Possible | Le client choisit ses champs |
| Cache HTTP | Naturel (GET, ETag) | Difficile (POST) |
| Outils .NET | Minimal API, Controllers | Hot Chocolate, Strawberry Shake |
| Complexité | Faible | Plus élevée (schéma, résolveurs, coût des requêtes) |

Pour ShopPulse, REST suffit : ressources simples, cache HTTP précieux. GraphQL se justifie quand des clients très différents (mobile, web, partenaires) veulent des formes de données très différentes.

## 2. HTTP Caching

Les en-têtes du module 5 s'appliquent aux réponses d'API : `Cache-Control` pour la fraîcheur, `ETag` pour la revalidation. Une API REST bien conçue fait de ses GET des ressources cachables. Les POST ne sont pas cachés, et un GET avec corps est à proscrire. `HttpClient` ne met pas en cache par défaut : ajoutez un cache applicatif (HybridCache, module 2) ou un `DelegatingHandler` qui gère `If-None-Match`. Le fil rouge applique les deux côtés : décorateur HybridCache dans l'API, politique « catalog » dans le Web.

## 3. ETags

Un ETag est une empreinte de la réponse. Le client la renvoie dans `If-None-Match` ; si elle est inchangée, le serveur répond **304 Not Modified sans corps** :

```csharp
var etag = $"\"product-{p.Id}-{p.Stock}\"";
if (ctx.Request.Headers.IfNoneMatch == etag)
       return Results.StatusCode(StatusCodes.Status304NotModified);
ctx.Response.Headers.ETag = etag;
ctx.Response.Headers.CacheControl = "no-cache";
```

Un hash du contenu (`SHA256` tronqué) convient aussi. Les ETags servent aussi à la **concurrence optimiste** : `If-Match` sur un PUT refuse (412) si la ressource a changé depuis.

**Vérifié** : `GET /api/products/1` renvoie `ETag: "product-1-49"` ; la même requête avec `If-None-Match` renvoie **304**. (Exercice 1 : observer dans l'onglet Réseau.)

## 4. Pagination

| | Offset (page=3) | Curseur (after=abc) |
|---|---|---|
| Simplicité | Élevée | Moyenne |
| Accès à une page précise | Oui | Non |
| Cohérence si les données changent | Mauvaise (doublons, trous) | Bonne |
| Performance sur grande table | Se dégrade (OFFSET énorme) | Stable (clé indexée) |

Pagination par curseur : `take = Math.Clamp(limit ?? 20, 1, 100)` (toujours borner), `GetAfterAsync(afterId, take + 1)` (+1 pour savoir s'il y a une suite), curseur opaque encodé en base64. Un curseur **opaque** peut changer de format sans casser personne ; validez-le (un curseur falsifié ne doit pas lever une 500). (Exercice 2.)

## 5. Infinite Scroll

Charger la page suivante quand l'utilisateur approche du bas, via `IntersectionObserver` (pas l'événement scroll) :

```javascript
export function observe(element, dotnetRef) {
   const io = new IntersectionObserver((entries) => {
       if (entries[0].isIntersecting) dotnetRef.invokeMethodAsync("LoadMore");
   }, { rootMargin: "300px" });
   io.observe(element);
   return { dispose: () => io.disconnect() };
}
```

Accessibilité : une liste infinie piège le clavier et empêche d'atteindre le pied de page — offrez un bouton « Charger plus » ou une pagination en alternative (`<noscript>` inclus), et annoncez les nouveaux éléments (module 8). (Documenté, non branché dans le fil rouge.)

## 6. Polling

Interroger le serveur à intervalle régulier : simple mais coûteux. En Blazor Server : `PeriodicTimer` + `CancellationTokenSource` annulé à la destruction du composant. Acceptable pour des données qui changent peu ; sinon, préférez la poussée.

## 7. WebSockets (SignalR)

Un canal bidirectionnel persistant ; en ASP.NET Core, on ne manipule presque jamais les WebSockets bruts : **SignalR** les encapsule (repli automatique SSE/long polling). Et Blazor Server utilise déjà SignalR pour son circuit.

Le fil rouge, en trois morceaux :

```csharp
// 1. L'API : hub + publisher (ShopPulse.Api)
public sealed class StockHub : Hub
{
       public Task Subscribe(int productId) =>
              Groups.AddToGroupAsync(Context.ConnectionId, $"product-{productId}");
}
public sealed class StockPublisher(IHubContext<StockHub> hub) : IStockPublisher
{
       public Task PublishAsync(int productId, int stock) =>
              hub.Clients.Group($"product-{productId}").SendAsync("StockChanged", productId, stock);
}

// 2. Le Web : connexion par circuit (ShopPulse.Infrastructure/StockFeed)
_conn = new HubConnectionBuilder()
       .WithUrl($"{cfg["Api:BaseUrl"]}hubs/stock")
       .WithAutomaticReconnect()
       .Build();

// 3. L'îlot StockWatcher (Features.Catalog) affiche le stock en temps réel
```

`StockFeed` est enregistré **Scoped** (une connexion par circuit) derrière le port `IStockFeed` (Application) — c'est lui qui remplace le `CatalogNotifier` du module 3. Pour beaucoup d'utilisateurs, une seule connexion Singleton qui diffuse en interne à tous les circuits réduit fortement le nombre de connexions sortantes.

**Vérifié** : `POST /api/stock {productId:1, stock:7}` → `"stock":7` dans l'API ; la page Web affiche « 7 en stock ». (Exercice 3 : ouvrir la fiche produit dans le navigateur, changer le stock via curl, voir l'affichage bouger sans rechargement.)

> **Piège majeur vécu** : `dotnet add package Microsoft.AspNetCore.SignalR.Client` a résolu la version **11.0.0** (dernière majeure) alors que l'application tourne sur le runtime .NET 10 — résultat : `MissingMethodException: IInvocationBinder.GetTarget(ReadOnlySpan<byte>)` qui faisait **crasher chaque circuit Blazor au démarrage** (tous les boutons morts, message client trompeur « Connection closed with an error », aucune exception visible dans les logs par défaut). Leçons : (1) les paquets `Microsoft.AspNetCore.*` doivent rester alignés sur la **même majeure que le runtime** ; (2) la gestion centralisée a permis le correctif en une ligne (`10.0.15`, version patchée et présente sur la machine) ; (3) le diagnostic a nécessité des logs `Microsoft.AspNetCore.SignalR=Debug` pour voir l'exception — en Information, le circuit meurt en silence.

## 8. Server-Sent Events (SSE)

Flux unidirectionnel serveur → client sur HTTP, avec reconnexion automatique intégrée. Plus simple que WebSockets quand le client n'a rien à envoyer. .NET 10 : `TypedResults.ServerSentEvents` (l'API exacte dépend de la version — vérifier, comme toujours). Comparaison : polling (aucune infrastructure), SSE (HTTP standard, reconnexion auto, notifications/flux), WebSocket/SignalR (connexion longue, bidirectionnel, chat/collaboratif/jeux).

## 9. Retries and Backoff

Réessayer avec attente croissante et un peu d'aléa (jitter), pour ne pas surcharger un service déjà en difficulté. Paquet officiel : `Microsoft.Extensions.Http.Resilience` (basé sur Polly) :

```csharp
builder.Services.AddHttpClient<IProductService, ProductApiClient>(...)
       .AddStandardResilienceHandler(o =>
       {
              o.Retry.MaxRetryAttempts = 3;
              o.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
              o.Retry.UseJitter = true;
              o.Retry.Delay = TimeSpan.FromMilliseconds(300);
              o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
              o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
       });
```

`AddStandardResilienceHandler` combine : limiteur de débit, délai total, retry, disjoncteur (circuit breaker), délai par tentative.

> **Règle d'or : ne réessayez que ce qui est idempotent.** Un GET se rejoue sans risque ; un `POST /orders` peut créer deux commandes. Pour les écritures : clé d'idempotence (concept 17) ou retry désactivé pour ce client. Réessayez sur 408, 429, 5xx et erreurs réseau ; jamais sur 400, 401, 403, 404.

## 10. Request Deduplication

Quand plusieurs composants demandent la même ressource simultanément, une seule requête part. Le `QueryClient` du module 3 le fait avec `Lazy<Task>` ; pour un HttpClient brut, un `InFlightDeduper` (dictionnaire de `Lazy<Task<string>>`, `WaitAsync(ct)`). Ne dédoublonnez que les requêtes **sûres (GET) et identiques** (même URL, mêmes en-têtes pertinents, même utilisateur).

## 11. Request Cancellation

Quand l'utilisateur quitte une page ou tape une nouvelle recherche, la requête devenue inutile doit être annulée. Le jeton circule de bout en bout : Minimal API (le `CancellationToken` est lié à la connexion client) ; côté composant, un `CancellationTokenSource` remplacé à chaque saisie, annulé dans `Dispose`. Combiné au debounce (module 2), cela évite les « réponses dans le désordre » où une réponse ancienne écraserait une récente.

## 12. Batching Requests

Regrouper plusieurs petits appels en un seul : `POST /products/batch` (valider : entre 1 et 100 identifiants). Côté client, une fenêtre de regroupement (DataLoader) rassemble les demandes faites pendant quelques millisecondes. Avec HTTP/2 et HTTP/3, les petites requêtes parallèles coûtent moins cher ; le batching reste utile pour réduire la charge serveur.

## 13. BFF Pattern

Le Backend For Frontend agrège, adapte les données et **garde les secrets**. **Blazor Server est déjà un BFF naturel** : le navigateur ne parle qu'à votre serveur, qui appelle les API avec ses propres jetons. Rèles du BFF : agréger (un appel interne par page, via `Task.WhenAll`), adapter, sécuriser (jetons, CORS, limitation), mettre en cache.

```csharp
public sealed class ProductPageService(IProductApi products, IStockApi stock, IReviewApi reviews)
{
       public async Task<ProductPageModel> LoadAsync(int id, CancellationToken ct)
       {
              var p = products.GetAsync(id, ct);
              var s = stock.GetAsync(id, ct);
              var r = reviews.TopAsync(id, 5, ct);
              await Task.WhenAll(p, s, r);          // en parallèle
              return new ProductPageModel(await p, await s, await r);
       }
}
```

## 14. CORS

Règle du navigateur qui bloque, par défaut, un script d'une origine appelant une autre origine. **Avec Blazor Server, CORS ne s'applique pas aux appels serveur → API** ; il apparaît seulement si du JavaScript du navigateur appelle directement l'API. Erreurs fréquentes : `AllowAnyOrigin()` avec des cookies (interdit par la spécification), refléter `Origin` sans liste blanche (équivalent à tout autoriser), croire que CORS protège l'API (un client non-navigateur l'ignore — l'authentification reste indispensable, module 7).

## 15. Handling Rate Limits

Deux côtés. **Limiter les entrants** (le fil rouge, sur l'API) :

```csharp
builder.Services.AddRateLimiter(o =>
{
       o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
       o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(
              partitionKey: ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
              factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) }));
});
app.UseRateLimiter();
api.RequireRateLimiting("api");
```

**Respecter les limites sortantes** : lire `Retry-After` sur un 429/503 — le gestionnaire de résilience le fait automatiquement. Derrière un proxy, configurez les en-têtes transférés (sinon tous les utilisateurs partagent l'IP du proxy). (Exercice 6 : dépasser 100 requêtes/minute → 429 avec `Retry-After`.)

## 16. Offline Queue

Les écritures faites hors ligne sont mises en file, puis **rejouées dans l'ordre**. Chaque entrée porte une clé d'idempotence :

```csharp
public sealed record QueuedAction(Guid Id, string Type, string PayloadJson, DateTime CreatedAt, int Attempts = 0);

// FlushAsync : pour chaque entrée
req.Headers.Add("Idempotency-Key", item.Id.ToString());   // un rejeu ne duplique rien
if (res.IsSuccessStatusCode || (int)res.StatusCode is >= 400 and < 500)
       await db.RemoveAsync("queue", item.Id.ToString());   // fait, ou définitivement refusé
else
       break;                                               // 5xx : on réessaiera plus tard
```

Un 4xx rejeté définitivement doit être **signalé à l'utilisateur** (« votre ajout n'a pas pu être appliqué »), pas supprimé en silence. `OfflineQueue` est dans Infrastructure, prêt à être branché (exercice 4 du module 5).

## 17. Error Handling

Une forme unique côté API (**Problem Details**, RFC 9457 — `AddProblemDetails()` dans le fil rouge), un message adapté côté UI (`ApiErrors.ToMessage` : 404 → « Cet élément n'existe plus », 429 → « Trop de requêtes, patientez un instant »…), des journaux pour l'équipe. `ErrorBoundary` pour qu'un composant en échec n'abatte pas la page (`_boundary?.Recover()`). Jamais de trace d'exception ni de message technique à l'utilisateur ; jamais de données personnelles dans les journaux.

## 18. Timeouts

Toute opération réseau a une limite de temps — sans cela, un service lent bloque des circuits entiers. Trois niveaux : par client HTTP (`AttemptTimeout`/`TotalRequestTimeout`, cf. concept 9), par opération (`CancellationTokenSource.CreateLinkedTokenSource` + `CancelAfter`), côté serveur (`AddRequestTimeouts` + `UseRequestTimeouts` — 15 s par défaut dans l'API du fil rouge). Distinguez délai de connexion, délai par tentative et délai total ; mesurez le 95e percentile de vos appels pour les fixer.

> **Piège de version vécu** : la syntaxe `new RequestTimeoutPolicy { ... }` du cours (écrite pour .NET 8) compile dans `Microsoft.AspNetCore.Http.Timeouts` en .NET 10 — le type a déménagé de namespace. Exactement le cas « vérifiez dans la documentation de votre version ».

## 19. API Contracts

Un contrat décrit exactement ce que l'API accepte et renvoie :

- **Types partagés** : le projet `ShopPulse.Contracts` est référencé par l'API et le client (le fil rouge a étendu `ProductDto` de `ImageUrl`/`Description` — on ajoute des champs, on ne retire jamais sans nouvelle version).
- **OpenAPI** : `Microsoft.AspNetCore.OpenApi` (génération intégrée .NET 9+), `MapOpenApi()` en dev — **vérifié : `/openapi/v1.json` répond 200**.
- Validation intégrée des Minimal APIs (.NET 10), versionnement (`/api/v1/` ou Asp.Versioning), tests de contrat (comparer le JSON OpenAPI généré à une référence détecte les ruptures).

> **Dette de sécurité résolue en direct** : la version du modèle épinglait `Microsoft.OpenApi 2.0.0`, signalée **vulnérable (NU1903, advisory GHSA-v5pm-xwqc-g5wc)** — `TreatWarningsAsErrors` a transformé l'alerte en erreur de build. Correction : montée de `Microsoft.AspNetCore.OpenApi` à la dernière version 10.0.x corrigée. La gestion centralisée des versions (module 4) a rendu le correctif trivial — un seul fichier.

## 20. HTTP/2 and HTTP/3

| Transport | HTTP/1.1 | HTTP/2 | HTTP/3 |
|---|---|---|---|
| Multiplexage | Non (6 connexions) | Oui | Oui |
| Blocage en tête de file | Oui | Oui (TCP) | Non (QUIC/UDP) |
| Compression d'en-têtes | Non | HPACK | QPACK |
| Reprise de connexion | Non | Au niveau TCP | Rapide (0-RTT) |

Kestrel prend en charge les trois (`HttpProtocols.Http1AndHttp2AndHttp3` — HTTP/3 exige TLS 1.3 et QUIC/msquic ; le navigateur découvre HTTP/3 via `Alt-Svc`). Conséquence pratique : avec HTTP/2+, les astuces HTTP/1 (concaténer les fichiers, domaines multiples) deviennent inutiles voire nuisibles. Pour SignalR, WebSockets reste le transport par défaut ; WebSockets sur HTTP/2 est pris en charge par Kestrel.

---

## Récapitulatif : ShopPulse après le module 6

| Besoin | Solution |
|---|---|
| Accès aux données | API REST + HttpClient typé |
| Listes | Pagination par curseur + défilement infini accessible (exercice) |
| Revalidation | ETag + 304 (vérifié) |
| Temps réel | SignalR (StockHub) → StockFeed → StockWatcher (vérifié) |
| Fiabilité | AddStandardResilienceHandler, timeouts, annulation |
| Écritures hors ligne | File IndexedDB + clés d'idempotence |
| Erreurs | Problem Details + ApiErrors + ErrorBoundary |
| Protection | RateLimiter (429 + Retry-After), CORS inutile côté serveur (BFF) |

**Les trois choses à retenir :**
1. **Blazor Server est un BFF** : le navigateur ne voit que votre serveur ; les secrets, les retries, la résilience vivent côté serveur, jamais dans le client.
2. **Fiabilité = idempotence + annulation + délais** : on ne retry que ce qui est sûr, on annule ce qui est devenu inutile, tout a une limite de temps.
3. **Le temps réel a un prix** : une connexion par circuit est simple ; à l'échelle, une connexion partagée qui diffuse en interne, ou SSE quand le client n'a rien à envoyer.

## Exercices du module

1. ✅ fait en pair programming : ETag sur `/api/products/{id}` et 304 vérifiés — à refaire dans l'onglet Réseau.
2. Pagination par curseur (remplacer l'offset) ; tester un curseur falsifié → 400, pas 500.
3. **StockFeed en direct** : ouvrir `/product/1`, puis `curl -X POST http://localhost:5299/api/stock -H "Content-Type: application/json" -d '{"productId":1,"stock":3}'` → le stock affiché change sans rechargement.
4. Arrêter l'API 5 s pendant une requête : observer les tentatives Polly puis la récupération.
5. Test : un POST rejoué avec la même `Idempotency-Key` ne crée qu'une commande.
6. Dépasser 100 requêtes/minute sur l'API → 429 avec `Retry-After`.
