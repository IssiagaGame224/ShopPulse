# Skills.md — Compétences acquises sur le projet ShopPulse

> **À quoi sert ce fichier.** L'inventaire des compétences réellement pratiquées et
> vérifiées pendant la construction de ShopPulse (cours « Frontend Engineering .NET 10 /
> Blazor Server », transformation en vraie boutique, évolutions production). Chaque
> compétence renvoie au chapitre `docs/` où elle a été vécue et prouvée — c'est la
> différence entre « connaître » et « avoir débogué ». Réutilisable comme référence
> pour de futurs projets : quoi réutiliser, où regarder, quels pièges éviter.

---

## 1. Frontend & Blazor Server

- **Rendu hybride** : Static SSR + `InteractiveServer` par page/composant, streaming
  SSR, îlots interactifs, hydratation — commencer statique, rendre interactif sur
  besoin (`docs/01`).
- **Circuit SignalR** : cycle de vie, reconnexion, ce qui survit (et ne survit pas)
  à une navigation, propagation de la culture et du jeton dans le circuit
  (`docs/01`, `docs/14`, `docs/24`).
- **Composants** : composition plutôt qu'héritage (`ChildContent`, `RenderFragment<T>`),
  `[EditorRequired]`, `CaptureUnmatchedValues`, Atomic Design (Atoms → Templates)
  (`docs/04`).
- **Gestion d'état** : état au plus bas niveau utile (URL, `Store<T>`, `QueryClient`,
  `EditContext`, `CascadingValue`), immuabilité (`record`, `with`), lifetimes DI
  (Scoped = état utilisateur, jamais Singleton) (`docs/03`).
- **Performance perçue** : LCP/INP/CLS, debounce, virtualisation, lazy loading,
  mesure avant optimisation (1,7 s → 0,1 s mesuré) (`docs/02`).
- **CSS** : propriétés logiques (RTL), jetons `--sp-*`, contrastes WCAG dans deux
  thèmes, layout e-commerce complet (en-tête, nav, footer, fiche produit type
  marketplace) (`docs/08`, `docs/16`).
- **Interop JS maîtrisée** : jamais pendant le prerender, `@key` sur les nœuds
  focalisés, montage d'un Payment Element Stripe post-rendu (`docs/17`).

## 2. Architecture

- **Monolithe modulaire** : une RCL par feature, `IShopModule` + composition dans
  l'hôte, frontières imposées par le compilateur (`docs/04`, ADR-004).
- **Tests d'architecture** : NetArchTest (Domain ne référence jamais ASP.NET…) —
  14 règles vérifiées en CI (`docs/04`).
- **Ports & adaptateurs** : `IProductService`, `IPaymentGateway`, `IOrderPlacer`…
  l'implémentation change (mémoire → EF → HTTP → Stripe), les composants non.
- **Versions centralisées** : `Directory.Packages.props`, `TreatWarningsAsErrors`,
  alignement des paquets sur la majeure du runtime (piège `MissingMethodException`).
- **ADR** : une décision structurante = un document (`docs/adr/`).

## 3. Backend & données

- **Minimal API .NET 10** : endpoints, Problem Details (RFC 9457), OpenAPI,
  Rate Limiting, Request Timeouts (`docs/06`, `docs/12`).
- **EF Core 10 + PostgreSQL** : migrations au démarrage, `DesignTimeDbContextFactory`,
  catégories relationnelles, filtres/tri/pagination **en SQL** jamais en mémoire,
  décrément de stock atomique (`docs/11`, `docs/12`).
- **Commandes réelles** : prix recalculés serveur, transaction, instantanés
  (adresse, prix, TVA, port), machine à états de statuts, restock à l'annulation
  (`docs/13`, `docs/15`, `docs/18`).
- **Idempotence** : `Idempotency-Key` sur la création de commande, Payment Intent
  idempotent (clé = orderId), remboursement avant transition de statut
  (`docs/13`, `docs/17`, `docs/20`).
- **Caching à étages** : `HybridCache` + invalidation par tag, `OutputCache`
  (vary query + Accept-Language), règle d'or « tout cache a une invalidation »
  (`docs/05`, `docs/12`).
- **Temps réel** : SignalR (`StockHub`), notifications de stock, invalidation de
  cache en cascade (`docs/06`).
- **Pattern Outbox** : messages écrits dans la transaction métier, worker de
  relance avec retry ×5 — reprise après panne SMTP démontrée (`docs/21`).

## 4. Sécurité

- **Principe directeur** : tout ce qui vient du navigateur est non fiable, y
  compris en Blazor Server (`docs/07`).
- **CSP appliquée** : nonce par requête (scripts, ImportMap, JSON-LD), passage de
  Report-Only au bloquant, resserrage final `style-src 'self'` sans
  `unsafe-inline`, vérifié en navigateur réel (`docs/22`, `docs/24`).
- **OIDC + PKCE** (Keycloak) : cookie `__Host-` (`HttpOnly`, `Secure`,
  `SameSite=Lax`, `Path=/` forcé), jetons côté serveur, refresh token avec verrou
  et marge, expiration inactivité + absolue (`docs/14`, `docs/21`).
- **JWT bearer API** : `MapInboundClaims=false` (sinon `sub` est renommé),
  politiques par rôle, 404 anti-énumération (`docs/14`, `docs/15`).
- **Divers** : antiforgery, validation du `returnUrl`, `HtmlSanitizerService`
  avant tout `MarkupString`, signature HMAC en temps constant (suivi invité),
  webhook Stripe signé = seule preuve de paiement, NuGetAudit bloquant à la
  build (`docs/07`, `docs/17`, `docs/20`).
- **Secrets** : user-secrets en dev, variables d'environnement en conteneur,
  jamais dans le dépôt.

## 5. Paiement & emails transactionnels

- **Stripe** : Payment Intents, Payment Element monté par interop, webhooks
  signés idempotents, remboursements, `IPaymentGateway` interchangeable avec
  simulateur dev (chemin réel non vérifié faute de clés — dit honnêtement)
  (`docs/17`, `docs/20`).
- **Emails** : MailKit → Mailpit en dev, emails de confirmation et de changement
  de statut, fiabilité par Outbox (`docs/13`, `docs/15`, `docs/21`).

## 6. Observabilité (les trois piliers + l'alerte)

- **Sondes** : `/health/live` vs `/health` détaillé par dépendance (`docs/19`, `docs/21`).
- **Logs structurés** : Serilog, propriété `Application`, `TraceId` par requête —
  les logs sont des données, pas des phrases (`docs/19`).
- **Métriques** : OpenTelemetry → `/metrics` Prometheus (API + Web) (`docs/22`).
- **Traces distribuées** : spans ASP.NET Core + HttpClient + Npgsql, propagation
  W3C `traceparent`, export OTLP conditionnel → Jaeger ; une page = une trace
  Web → API → SQL (`docs/25`).
- **Alerting** : Prometheus + règles (`ServiceDown`, `HighHttp5xxRate`,
  `SlowRequestsP95`) → Alertmanager → email Mailpit — prouvé par panne réelle
  (`[FIRING]` et `[RESOLVED]` reçus) (`docs/26`).

## 7. Tests (la pyramide, vérifiée)

- **Architecture** : NetArchTest, 14/14 (`docs/04`).
- **Intégration** : `WebApplicationFactory` + Testcontainers PostgreSQL + vrai
  Keycloak — 16 tests ; piège : le HttpClient de la factory route en mémoire,
  Keycloak exige un vrai client (`docs/20`).
- **E2E Playwright** : tunnel d'achat complet, compte, back-office, refresh token,
  CSP — cibles configurables (`SHOPPULSE_WEBURL`/`SHOPPULSE_APIURL`), 7/7
  (`docs/09` → `docs/26`).
- **Accessibilité automatisée** : axe-core, 5 pages × 3 langues, RTL inclus, 5/5
  (`docs/08`).
- **Robustesse des tests** (leçons vécues) : jamais de données consommables en dur
  (produits choisis en stock dynamiquement — `TestCatalog`), jamais de dépendance
  à la culture de l'OS, sélecteurs stables, sondage plutôt que sleep (`docs/24`-`25`).

## 8. Accessibilité & internationalisation

- **WCAG 2.2 AA** : HTML sémantique, skip links, `aria-live`, focus géré, clavier
  d'abord (`docs/08`).
- **i18n réelle** : locales `/fr|en|ar` par middleware (PathBase + culture),
  cookie `sp_locale`, pluriels CLDR (6 formes arabes), `<bdi>`, formats par
  culture, `Accept-Language` transmis à l'API (`docs/08`, `docs/24`).
- **RTL** : propriétés logiques CSS partout, miroir vérifié par axe.

## 9. DevOps, Docker & exploitation

- **Compose multi-services** (9 services) : DNS split-horizon autour de Keycloak
  (`--hostname-backchannel-dynamic`), healthchecks + `depends_on: service_healthy`,
  volumes (données, Data Protection, sauvegardes) (`docs/09`).
- **Dockerfiles multi-étapes**, `.dockerignore` (y compris `bin-local/` — piège
  NETSDK1152 vécu), `.gitattributes` (`*.sh` en LF pour les conteneurs) (`docs/24`).
- **Sauvegardes** : pg_dump format custom, écriture atomique tmp+mv, vérification
  `pg_restore --list`, rétention, **restauration exercée et prouvée par comptages**
  — une sauvegarde non restaurée n'est qu'une hypothèse (`docs/23`).
- **Publication** : `dotnet publish -c Release`, jamais de `dotnet watch` en
  production (`docs/09`).

## 10. Méthode de débogage (les autopsies qui ont le plus appris)

1. **Cascade d'erreurs Razor** : 26 erreurs pour une ligne — remonter à la
   première, ne jamais paniquer devant le nombre (`docs/01`).
2. **SignalR 11 sur runtime 10** : application morte en silence, diagnostiquée au
   navigateur automatisé + logs Debug — aligner les majeures (`docs/06`).
3. **Service worker qui tue les circuits** : le majordome qui servait des pages
   périmées (`docs/05`).
4. **DNS split-horizon Keycloak** : un immeuble, deux entrées (`docs/09`).
5. **`listen()` gelé dans le noyau Windows** : thread principal bloqué dans
   l'appel natif Winsock (prouvé par `dotnet-stack`), processus zombies
   incluables, ports fantômes invisibles dans netstat → contournement par
   conteneurs, sans jamais bloquer la vérification (`docs/24`).
6. **Culture de l'OS comme dépendance cachée** : tests verts en dev (Windows
   fr-FR), clés de traduction brutes en conteneur (culture invariante) — le
   middleware de locale doit tout couvrir (`docs/24`).
7. **Course asynchrone** : panier restauré après le premier rendu, bouton figé —
   tout état restauré en asynchrone doit être *observé* (`docs/24`).
8. **Méthode générale** : reproduire → isoler → prouver (dump de threads, requête
   HTTP, comptage SQL) → corriger → **verrouiller par un test**.

## 11. Collaboration & processus

- **Workflow Git d'équipe** : `main` protégée, une branche courte par feature
  (`feature/backend-*`, `feature/frontend-*`, full-stack sans préfixe), PR
  obligatoire validée par le propriétaire avant merge, branche supprimée après
  merge (`AGENTS.md` §9).
- **Documentation vivante** : après chaque étape vérifiée, un chapitre `docs/`
  en français — concepts, marqueurs « Piège » / « Limite », bilan avec les scores
  réels. La doc est écrite **après** la preuve, jamais avant.
- **La mesure avant l'opinion** : chaque affirmation adossée à une requête, un
  test ou un chiffre (1,7 s → 0,1 s ; 14/14 ; 7/7 ; 5/5…).
- **Dire ce qui n'est pas vérifié** : « chemin Stripe réel non testé faute de
  clés » — une limite documentée vaut mieux qu'une certitude fausse.

## 12. Les 5 règles d'or (bilan du cours, vécues)

1. Commencer statique, rendre interactif sur besoin.
2. Tout cache est une promesse d'invalidation.
3. Le navigateur (DOM, cookies, focus) n'est jamais digne de confiance.
4. Ce qui n'est pas testé automatiquement régresse.
5. Une seule source de vérité par donnée.
