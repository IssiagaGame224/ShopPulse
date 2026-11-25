# 25 — Traces distribuées : OpenTelemetry → Jaeger

> **Contexte.** L'observabilité avait déjà les sondes et les logs (docs/19) puis les
> métriques Prometheus (docs/22). Il manquait la troisième jambe : les **traces** — le
> chemin d'une requête à travers les services. Quand une page catalogue met 800 ms,
> les métriques disent « ça ralentit », les logs disent « voici des événements » ; la
> trace dit **où** le temps se passe : Web, API, ou SQL.

## Le câblage : trois sources de spans, un export conditionnel

Dans les deux `Program.cs`, à côté des métriques existantes :

```csharp
builder.Services.AddOpenTelemetry()
       .ConfigureResource(r => r.AddService("ShopPulse.Api"))   // nom dans Jaeger
       .WithMetrics(m => m /* … inchangé … */)
       .WithTracing(t =>
       {
              t.AddAspNetCoreInstrumentation()   // requêtes entrantes
               .AddHttpClientInstrumentation()   // appels sortants (Web → API)
               .AddSource("Npgsql");             // requêtes SQL (ActivitySource de Npgsql)
              if (builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is not null)
                     t.AddOtlpExporter();        // gRPC 4317, lu depuis l'environnement
       });
```

Deux décisions :

- **Export conditionnel** : sans `OTEL_EXPORTER_OTLP_ENDPOINT`, aucun exportateur OTLP
  n'est enregistré — un `dotnet run` local sans collecteur ne produit ni erreurs de
  connexion ni latence d'export. En conteneur, compose le fournit
  (`http://jaeger:4317`) ; en local, `set OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`.
- **La propagation est gratuite** : l'instrumentation HttpClient écrit l'en-tête W3C
  `traceparent` sur chaque appel sortant, l'instrumentation ASP.NET Core le lit à
  l'arrivée — les spans Web et API atterrissent dans **la même trace**, sans code.

Le collecteur est un service compose (`jaegertracing/jaeger`, v2 — récepteur OTLP
natif) : UI sur `http://localhost:16686`, OTLP gRPC sur 4317.

## Vérification : une page, une trace, trois étages

Après `GET /fr/catalog`, l'API de requête Jaeger (`/api/v3/traces`) montre une trace
unique contenant :

```
ShopPulse.Web :: GET /catalog          ← page entrante
ShopPulse.Web :: GET                   ← appel HttpClient vers l'API
ShopPulse.Api :: GET /api/categories   ← requête API (enfant, via traceparent)
ShopPulse.Api :: postgresql            ← requête SQL (Npgsql)
ShopPulse.Api :: GET /api/products/page
ShopPulse.Api :: postgresql
```

100 traces reçues sur la fenêtre de test, dont les traces multi-services du catalogue.
Non-régression : build 0/0, architecture 14/14, **E2E 7/7 sur trois runs consécutifs**,
axe **5/5**.

> **Piège** : sans `ConfigureResource(r => r.AddService(...))`, les spans arrivent
> sous le nom `unknown_service:dotnet` — indistinguables entre Web et API. Le nom de
> service est une donnée de la ressource OTel, pas du collecteur.

> **Piège** : l'API de requête de Jaeger v2 n'est plus `/api/traces` (v2 de l'API,
> 404) mais **`/api/v3/traces`**, avec `query.service_name`, `query.start_time_min`
> et `query.start_time_max` au format RFC 3339 — les anciens noms de paramètres
> répondent « min and max start time are required ».

**Robustesse des E2E, au passage** : les produits achetés par les tests (ids 2, 9, 10
en dur) étaient arrivés à **stock 0** après des dizaines de runs → échecs 409
intermittents sans rapport avec le code. Helper `TestCatalog.InStockProductIdAsync` :
chaque classe choisit dynamiquement un produit en stock, avec un décalage (`skip`)
différent — les classes tournent en parallèle et deux achats sur le même produit
fausseraient l'assertion « stock décrémenté d'exactement 1 ». Trois runs 7/7 stables.

## Bilan

- les trois piliers sont en place : sondes + logs (docs/19), métriques (docs/22),
  traces (ce chapitre) — chacun répond à une question différente ;
- une trace distribuée se lit comme le récit d'une requête : page → appels HTTP →
  SQL, avec les durées à chaque étage ;
- l'instrumentation est déclarative et la propagation automatique (W3C `traceparent`) :
  le seul code métier à écrire est… aucun.

**Limites** (volontaires) : stockage Jaeger **en mémoire** (les traces meurent avec le
conteneur — un backend persistant comme Elasticsearch est une évolution) ; échantillonnage
à 100 % (acceptable en dev, à régler en production) ; UI Jaeger non sécurisée (à
protéger par le réseau, comme `/metrics`) ; pas de pipeline de collecteur intermédiaire
(batching, filtrage) — l'export est direct application → Jaeger.
