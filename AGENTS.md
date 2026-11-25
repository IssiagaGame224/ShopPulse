# AGENTS.md — Projet ShopPulse

> Fichier destiné aux agents de codage IA. Il suppose que le lecteur ne connaît
> rien du projet. Tout le contenu du projet (code, commentaires, documentation,
> interface) est en **français** : rédiger dans cette langue.

## 1. Vue d'ensemble du projet

ShopPulse est une **boutique e-commerce complète** construite avec **.NET 10,
Blazor Server et PostgreSQL** : catalogue, fiche produit, panier, tunnel de
commande avec paiement Stripe, comptes clients (Keycloak/OIDC), back-office
(rôle `stock-manager`), stock temps réel (SignalR), emails transactionnels
(Mailpit en dev), accessibilité WCAG 2.2 AA et internationalisation fr/en/ar.

Le répertoire est à la fois :

- **un espace d'apprentissage** : le projet était le fil rouge du cours
  « Frontend Engineering avec .NET 10 et Blazor Server — 8 domaines, 160
  concepts » (`ShopPulse_Cours_261117_141854.pdf`, 100 pages, extractible par
  `pdftotext` ; `frontend_engineering_160_concepts.md` liste les concepts) ;
- **une matière première pour un livre** : chaque étape terminée et vérifiée a
  donné un chapitre dans `docs/`, puis un manuscrit complet dans
  `docs/manuscrit/` (chapitres 01 à 10 + conclusion) et `Livre/` (versions
  illustrées par chapitre + PDF final).

**État d'avancement : projet terminé.** Les 8 modules du cours (160 concepts),
le bonus déploiement Docker, puis la transformation en vraie boutique en 6
phases + refonte design sont achevés et vérifiés :

1. PostgreSQL 17 + EF Core 10 (migrations au démarrage) — `docs/11`
2. Catégories relationnelles, filtres/pagination/tri SQL, décrément de stock
   atomique — `docs/12`
3. Commandes persistées (prix recalculés serveur, transaction, idempotence par
   `Idempotency-Key`, emails Mailpit) — `docs/13`
4. Comptes clients (JWT bearer `MapInboundClaims=false`, `UserId`=sub,
   `/orders/me`, 404 anti-énumération, propagation du jeton Web→API en
   circuit, cookie `__Host-` à `Path=/`) — `docs/14`
5. Back-office (produit enrichi `Sku`/`OldPrice`/`Features` jsonb, `/api/admin/*`
   sous `CanManageStock`, machine de statuts + emails) — `docs/15`
6. Paiement Stripe (`IPaymentGateway` interchangeable : Stripe.net 49.2.0 si
   `Stripe:SecretKey` configuré, sinon simulateur dev ; Payment Intent
   idempotent ; **webhook signé = seule preuve de paiement** ; le chemin Stripe
   réel n'a pas été vérifié faute de clés) — `docs/17`

Plus : refonte design e-commerce (`docs/16`), livraison + TVA + restock
(`docs/18`), observabilité health checks + Serilog (`docs/19`), remboursements
+ tests d'intégration + suivi invité signé HMAC (`docs/20`), refresh token +
pattern Outbox + sondes live/ready (`docs/21`), CSP appliquée + i18n admin +
métriques Prometheus (`docs/22`), **sauvegardes PostgreSQL** (service `backup`
dans compose : pg_dump quotidien format custom, écriture atomique tmp+mv,
vérification `pg_restore --list`, rétention 7, restauration prouvée par
comptages — `docs/23`), **CSP `style-src 'self'`** (plus d'`unsafe-inline` —
aucun style inline dans l'app ; 2 tests E2E CSP permanents ; cookie `sp_locale`
enfin implémenté : les URL sans préfixe recevaient la culture de l'OS — clés
brutes en conteneur ; course panier/`CartSync` corrigée dans le tunnel ;
E2E 7/7, axe 5/5 — `docs/24`), **traces distribuées OTel → Jaeger** (spans
ASP.NET Core + HttpClient + `AddSource("Npgsql")`, export OTLP conditionné par
`OTEL_EXPORTER_OTLP_ENDPOINT`, `ConfigureResource(AddService)` sinon
`unknown_service:dotnet` ; service `jaeger` dans compose, UI 16686, API de
requête `/api/v3/traces` ; trace Web→API→SQL vérifiée — `docs/25`), **alerting
Prometheus + Alertmanager** (scrape de `/metrics`, 3 règles — `ServiceDown`,
`HighHttp5xxRate`, `SlowRequestsP95` — notification email via Mailpit, chaîne
prouvée par arrêt réel du conteneur web : `[FIRING]` reçu dans Mailpit ;
configs dans `docker/prometheus/` et `docker/alertmanager/` — `docs/26`),
**CI GitHub Actions** (2 jobs requis sur chaque PR : build + tests rapides,
E2E + A11y sur pile compose ; health check NpgSql résolu paresseusement —
`docs/27`), **avis clients** (table `reviews` : unicité `(ProductId, UserId)`
et `CHECK` 1–5 en base, upsert par client, auteur = `preferred_username` du
jeton, agrégat GROUP BY sans N+1 sur `Product.AverageRating/ReviewCount`,
îlot `ProductReviewsSection` sur la fiche, étoiles `ProductCard`, modération
`/admin/reviews` trilingue, E2E en collection séquentielle — `docs/28`).

Évolutions possibles (non faites) : alertes métier (stock bas, ancienneté du
dernier dump), backend de traces persistant, sauvegarde externalisée (règle 3-2-1),
i18n de la fiche produit publique, pagination des avis.

## 2. Structure du dépôt

Solution **monolithe modulaire** au format `ShopPulse.slnx` (format par défaut
de .NET 10), avec versions NuGet centralisées (`Directory.Packages.props`) et
propriétés communes (`Directory.Build.props` : `net10.0`, C# 14,
`Nullable`/`ImplicitUsings` activés, `TreatWarningsAsErrors=true`, audit
NuGet bloquant sur vulnérabilités ≥ moderate).

```
src/
  ShopPulse.Domain/            # Modèles purs (Product, Order, Pricing) — aucune
                               # référence ASP.NET (imposé par les tests d'architecture)
  ShopPulse.Application/       # Ports : IProductService, IOrderPlacer,
                               # IPaymentGateway, IOrderAdmin… + StoreConfig
  ShopPulse.Infrastructure/    # Adaptateurs : Persistence/ (EF Core, migrations,
                               # seed), clients HTTP (ProductApiClient…), Stripe,
                               # SMTP, cache, sanitizer HTML
  ShopPulse.Contracts/         # DTO partagés API ↔ clients (records)
  ShopPulse.SharedKernel/      # Types transverses sans dépendance : Store<T>,
                               # QueryClient, Result, IClock…
  ShopPulse.Modularity/        # IShopModule + ModuleCatalog + PluginLoader
  ShopPulse.UI/                # RCL design system : jetons CSS --sp-*, composants
                               # Atomic Design (Atoms/Molecules/Organisms/Templates),
                               # ressources i18n SharedResource.{fr,en,ar}.resx
  ShopPulse.Features.Catalog/  # RCLs métier (pages + composants + module DI) :
  ShopPulse.Features.Cart/     #   chaque feature enregistre SES services via
  ShopPulse.Features.Checkout/ #   CatalogModule : IShopModule (frontières nettes)
  ShopPulse.Features.Account/
  ShopPulse.Features.Admin/
  ShopPulse.Api/               # Minimal API : /api/products, /api/orders,
                               # /api/admin/*, /webhooks/stripe, /hubs/stock
                               # (SignalR), /health, /metrics, OutboxWorker
  ShopPulse.Web/               # Hôte Blazor Server : OIDC, sécurité, CSP,
                               # composition des modules via AddAdditionalAssemblies
tests/
  ShopPulse.Architecture.Tests/  # NetArchTest : règles de dépendances (14 tests)
  ShopPulse.Api.Tests/           # Intégration : WebApplicationFactory +
                                 # Testcontainers PostgreSQL + vrai Keycloak (16 tests)
  ShopPulse.E2E/                 # Playwright : checkout, compte, admin, refresh
                                 # token (4 tests + utilitaire Screenshots)
  ShopPulse.A11y/                # axe-core : 5 pages × fr/en/ar (RTL inclus)
docs/                    # Chapitres 00 à 27 (un par étape), adr/, manuscrit/, images/
Livre/                   # Chapitres illustrés du livre + PDF final
docker/keycloak/         # realm-shoppulse.json importé au démarrage
docker/postgres/         # backup.sh (sauvegardes périodiques, service « backup »)
docker/prometheus/       # prometheus.yml + alert.rules.yml (alerting, docs/26)
docker/alertmanager/     # alertmanager.yml (notification email via Mailpit)
scripts/dev.ps1          # Démarrage dev Windows (conteneurs + build Release + 2 fenêtres)
docker-compose.yml       # Pile complète : db, backup, keycloak, mailpit, api, web,
                         # jaeger (traces), prometheus + alertmanager (alerting)
Skills.md                # Inventaire des compétences pratiquées (référence
                         # réutilisable pour de futurs projets)
```

## 3. Pile technique et architecture d'exécution

- **Runtime : .NET 10 (`net10.0`), C# 14, Blazor Server** (Static SSR +
  `InteractiveServer` via circuit SignalR). Aucun C# ne s'exécute dans le
  navigateur. Le mode de rendu se choisit **par page ou par composant** ; ne
  jamais tout rendre interactif par défaut.
- **API** : ASP.NET Core Minimal API, SignalR (`StockHub`), JWT Bearer,
  Rate Limiting, Request Timeouts, Problem Details (RFC 9457), OpenAPI en dev.
  Top-level statements + `public partial class Program;` pour
  `WebApplicationFactory`.
- **Données** : PostgreSQL 17, EF Core 10 (Npgsql 10.0.4), migrations appliquées
  au démarrage (idempotent, acceptable pour une instance unique),
  `DesignTimeDbContextFactory` pour `dotnet ef` depuis Infrastructure.
- **Cache** : `HybridCache` côté API (décorateur `CachedProductService`,
  invalidation par tag `"catalog"` à chaque écriture) ; `OutputCache` côté Web
  (politique `catalog` : vary query + `Accept-Language`, tag pour invalidation).
- **Identité** : Keycloak (OIDC + PKCE), cookie `__Host-shoppulse` chiffré
  (`HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/` forcé), jetons côté serveur
  (`UserTokenStore` + refresh avec verrou), expiration inactivité 20 min +
  absolue 8 h.
- **Paiement** : `IPaymentGateway` — Stripe (Payment Intents, webhooks signés)
  ou `FakePaymentGateway` + endpoint `/fake-confirm` mappé **uniquement** sans
  clés.
- **Emails** : MailKit → Mailpit en dev ; **pattern Outbox** (`outbox_messages`
  écrit dans la transaction métier, `OutboxWorker` poll 2 s, retry ×5).
- **Observabilité** : Serilog structuré (propriété `Application`, `TraceId`),
  `/health/live` (processus) vs `/health` (JSON détaillé des dépendances),
  métriques OpenTelemetry → Prometheus sur `/metrics`, traces distribuées
  OTLP → Jaeger (UI 16686), alerting Prometheus → Alertmanager → email Mailpit.
- **Conteneurs** : `docker-compose.yml` = 9 services (db, backup, keycloak,
  mailpit, api, web, jaeger, prometheus, alertmanager) avec DNS split-horizon
  autour de Keycloak (`--hostname-backchannel-dynamic`) et volume Data
  Protection pour le Web.

## 4. Build et exécution

Prérequis : SDK **.NET 10**, Docker Desktop, navigateur Chromium (tests E2E/axe).

```bash
# Build complet (vérifié : 0 avertissement, 0 erreur avec TreatWarningsAsErrors)
dotnet build ShopPulse.slnx

# Développement local (3 terminaux)
docker compose up -d db mailpit keycloak     # infrastructure seule
cd src/ShopPulse.Api && dotnet run           # API  → http://localhost:5299
cd src/ShopPulse.Web && dotnet run           # Web  → http://localhost:5026
# (l'API applique migrations + seed au démarrage)

# Ou tout-en-un sous Windows
powershell -ExecutionPolicy Bypass -File scripts/dev.ps1

# Toute la pile en conteneurs
docker compose up -d --build                 # ports 5026 (web), 5299 (api),
                                             # 8080 (keycloak), 5432 (db), 8025 (mailpit)

# Publication (jamais en dotnet watch pour la production)
dotnet publish -c Release -o out

# Audit des dépendances (déjà bloquant à la build via Directory.Build.props)
dotnet list package --vulnerable --include-transitive
```

Accès de développement :

| Quoi | URL / identifiants |
|---|---|
| Boutique | http://localhost:5026 (redirige vers `/fr/`) |
| Mailpit (emails capturés) | http://localhost:8025 |
| Keycloak | http://localhost:8080 — `demo`/`demo1234` (stock-manager), `client`/`client1234` |
| Paiement sans clés Stripe | bouton « Simuler le paiement (démo) » ; avec clés : `dotnet user-secrets set "Stripe:SecretKey" sk_test_...` (voir `docs/17`) |

## 5. Stratégie de tests

Pyramide vérifiée (les serveurs API + Web doivent tourner pour E2E et A11y) :

```bash
dotnet test tests/ShopPulse.Architecture.Tests   # 14 règles NetArchTest (rapide, sans infra)
dotnet test tests/ShopPulse.Api.Tests            # 16 tests d'intégration
                                                 # (WebApplicationFactory + Testcontainers
                                                 # PostgreSQL + vrai Keycloak — Docker requis)
dotnet test tests/ShopPulse.E2E                  # 4 parcours Playwright
                                                 # (checkout, compte, admin, refresh token)
dotnet test tests/ShopPulse.A11y                 # axe-core WCAG 2.2 AA : 5 pages × 3 langues
```

- E2E et A11y acceptent des cibles configurables : `SHOPPULSE_APIURL` /
  `SHOPPULSE_WEBURL`.
- `Screenshots.cs` (E2E) est un utilitaire de capture visuelle, à lancer
  ponctuellement via `--filter Screenshots`.
- Vérification manuelle attendue pour certains parcours : navigation clavier
  pure, lecteur d'écran, onglet Réseau, injection XSS.

## 6. Conventions de code (à respecter dans tout ajout)

- **Langue** : documentation et textes d'interface en français ; l'anglais et
  l'arabe passent par `IStringLocalizer` + `SharedResource.{fr,en,ar}.resx`
  (73 clés). Toute nouvelle clé doit exister dans les 3 langues.
- **Marqueurs du cours** : « Piège » signale une erreur fréquente, « Limite »
  ce que le code ne couvre volontairement pas. Les conserver et les utiliser
  dans tout ajout de documentation ou de commentaire.
- **Injection de dépendances** : **Scoped par défaut** pour tout état
  utilisateur (panier, favoris, formulaires) ; un Singleton ne porte jamais
  d'état utilisateur (`CatalogNotifier`, bus sans état, est la seule exception
  métier).
- **État au plus bas niveau utile** : filtres/page dans l'URL, liste produits
  dans un `QueryClient`, formulaire dans `EditContext`, thème en
  `CascadingValue`.
- **CSS** : propriétés logiques (`margin-inline-start`, pas `margin-left`)
  pour le RTL ; jetons `--sp-*` du design system ; contraste WCAG 2.2 AA
  (texte 4,5:1, bordures/focus 3:1) dans les deux thèmes.
- **ARIA en Razor** : jamais d'attribut booléen nu — écrire
  `aria-expanded="@(_open ? "true" : "false")"`.
- **Interop JS / focus** : `FocusAsync` et l'interop JS attendent
  `OnAfterRenderAsync` ; jamais de `showModal()` pendant le prerender ;
  `@key` sur les nœuds focalisés.
- **Réseau** : tout cache a une règle d'invalidation ; tout appel réseau a un
  délai, une reprise et une annulation ; **ne jamais réessayer une écriture
  non idempotente** ; les prix sont toujours recalculés côté serveur.
- **Composants** : composition plutôt qu'héritage (`ChildContent`,
  `RenderFragment<T>`), `[EditorRequired]` pour les paramètres obligatoires,
  `[Parameter(CaptureUnmatchedValues = true)]` pour laisser passer `class` et
  `aria-*`.
- **Règle de rédaction** : après chaque étape terminée et vérifiée (build +
  exécution OK), rédiger ou mettre à jour le chapitre correspondant dans
  `docs/`, en français, avec une section par concept, les marqueurs « Piège »
  et « Limite », et un bilan.

## 7. Sécurité

Principe directeur : **tout ce qui vient du navigateur est non fiable**, y
compris dans Blazor Server.

- Blazor encode par défaut (`@valeur`) ; `MarkupString` n'est utilisé qu'avec
  `HtmlSanitizerService`.
- Cookie `__Host-` `HttpOnly` `Secure` `SameSite=Lax`, jetons côté serveur,
  antiforgery, validation du `returnUrl` (anti open redirect), expiration de
  session (inactivité 20 min + absolue 8 h).
- **CSP appliquée** (bloquante) avec nonce par requête (le `<ImportMap>` exige
  le nonce ; le JSON-LD aussi), `frame-ancestors 'none'`, HSTS hors dev.
- API : JWT bearer (`MapInboundClaims=false` — sinon `sub` est renommé),
  RateLimiter (100 req/min par utilisateur/IP), CORS strict, 404 plutôt que
  403 pour ne pas révéler l'existence d'une commande, signature HMAC en temps
  constant pour le suivi invité.
- Webhook Stripe : la signature validée est la **seule** preuve de paiement.
- NuGetAudit bloquant à la build (`Directory.Build.props`, NU1901–NU1904 en
  erreur).
- Secrets : `dotnet user-secrets` en dev, variables d'environnement en
  conteneur, **jamais dans le dépôt** (le `dev-secret-shoppulse` de
  docker-compose et le mot de passe PostgreSQL sont des valeurs de dev
  uniquement, documentées comme telles).

## 8. Accessibilité et internationalisation

- Objectif **WCAG 2.2 niveau AA**, vérifié par axe-core (matrice 5 pages ×
  3 langues, y compris le miroir RTL de l'arabe).
- Locales `/fr/...`, `/en/...`, `/ar/...` : middleware `LocalePathMiddleware`
  (préfixe → `PathBase` + culture, cookie `sp_locale`, `Accept-Language` ; ne
  jamais rediriger `/api` ni `/_blazor`).
- HTML sémantique d'abord, skip links via `a11y.js` (car `<base href="/">`
  casse les ancres), `LiveAnnouncer` (`aria-live` injectés dans `App.razor`),
  pluriels CLDR (6 formes en arabe via `PluralRules.cs`), `<bdi>` pour isoler
  le texte latin en arabe.
- Le client HTTP transmet manuellement `Accept-Language` à l'API.

## 9. Workflow Git (obligatoire pour tout agent)

Adopté après coup (le projet a historiquement été développé sur `main`) :

- **`main` est protégée** : jamais de commit direct. Elle reste toujours verte
  (build + tests) et déployable.
- **Une branche courte par feature**, créée depuis `main` à jour :
  `feature/backend-<nom>` (API, infra, données), `feature/frontend-<nom>`
  (Web, UI, composants), `feature/<nom>` sans préfixe pour le full-stack.
  Pas de branches permanentes par agent : deux branches longues divergent et
  finissent en conflits — l'identité de l'agent vit dans le nom de branche et
  la PR.
- **Une feature full-stack = une seule branche, une seule PR.** Une feature
  est atomique : si l'API et le Web doivent changer ensemble pour rester
  verts, ils changent dans la même PR (sinon `main` passe au rouge entre les
  deux merges).
- **PR obligatoire vers `main`** : l'agent crée la PR (`gh pr create`), la
  décrit (quoi, pourquoi, vérifications), puis **demande la validation au
  propriétaire dans la conversation**. Jamais de merge sans accord explicite.
  Après merge : suppression de la branche (`gh pr merge --delete-branch`).
- **CI obligatoire** (`.github/workflows/ci.yml`, docs/27) : deux jobs —
  *Build + tests rapides* (build, NetArchTest, intégration Testcontainers +
  Keycloak) et *E2E + A11y* (pile compose complète + Playwright). Les deux
  statuts sont **requis** par la protection de `main` : une PR rouge ne se
  fusionne pas. Le message de commit suit le style du dépôt : français,
  première ligne résumée, corps en puces (quoi + preuves : scores de tests).

## 10. Points de vigilance pour un agent

- **Ne pas inventer de fonctionnalité** : le projet suit le cours PDF puis les
  phases documentées dans `docs/`. Vérifier `docs/` avant d'ajouter quoi que
  ce soit.
- **Alignement des versions** : les paquets `Microsoft.AspNetCore.*` et
  `Microsoft.Extensions.*` doivent rester sur la **même majeure que le runtime
  installé** (un `dotnet add package` sans version peut résoudre une majeure
  supérieure → `MissingMethodException` qui tue les circuits Blazor en
  silence). Versions centralisées dans `Directory.Packages.props` ; vérifier
  avec `dotnet --list-runtimes`.
- Les API .NET évoluent d'une version à l'autre : vérifier la documentation
  .NET 10 pour toute API dépendante de la version (ex. `RequestTimeoutPolicy`
  a déménagé de namespace entre .NET 8 et 10).
- **Pièges documentés à ne pas réintroduire** :
  - `using Serilog` rend `ILogger` ambigu → qualifier ;
  - le HttpClient de `WebApplicationFactory` route tout en mémoire → Keycloak
    exige un vrai `HttpClient` dans les tests d'intégration ;
  - le cookie `__Host-` exige `Path=/` — le PathBase de la locale (`/fr`)
    casserait la connexion en boucle ;
  - le bouton Rechercher de l'en-tête ne doit pas porter `sp-btn--primary`
    (sélecteur E2E du CTA panier) ;
  - rembourser **avant** la transition de statut d'annulation d'une commande
    payée ;
  - `scripts/dev.ps1` build en Release car le dossier `bin/Debug` peut rester
    verrouillé par d'anciens processus Windows (d'où les dossiers `bin-local/`
    présents dans le dépôt — artefacts de build, ne pas les modifier).
- Les deux PDF à la racine ne sont pas tous indexables : le cours est
  extractible par `pdftotext` ; le diaporama « Architecture Universelle » est
  composé d'images (le visualiser page par page).
- Les numéros de version de paquets cités dans le cours sont des exemples :
  prendre les dernières versions stables au moment de l'installation.
