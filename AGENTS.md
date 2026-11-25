# AGENTS.md — Projet ShopPulse

## Vue d'ensemble du projet

Ce répertoire est à la fois **un espace d'apprentissage** (cours + projet fil rouge construit module par module) et **une matière première pour un livre informatique** : après chaque étape/module terminé et vérifié, un chapitre de documentation est rédigé dans `docs/`. Tout le contenu est en français.

ShopPulse est le « projet fil rouge » d'un cours complet de *Frontend Engineering* : **« Frontend Engineering avec .NET 10 et Blazor Server — 8 domaines, 160 concepts »** (document du 16 novembre 2026). ShopPulse est une mini-boutique (catalogue, fiche produit, panier, tableau de bord de stock en temps réel, back-office) que chaque module du cours enrichit, jusqu'à obtenir une application modulaire, sécurisée, accessible et multilingue (français, anglais, arabe).

L'état d'avancement : **COURS COMPLET + BONUS DÉPLOIEMENT + TRANSFORMATION EN VRAIE BOUTIQUE TERMINÉE** — 8 modules (160 concepts) + Docker Compose vérifié. Chapitres `docs/00` à `docs/09` + plan de manuscrit `docs/10-plan-de-manuscrit.md`. Feuille de route « vraie boutique » **6 phases + refonte design, toutes terminées** : **phases 1 à 5** — (1) PostgreSQL 17 + EF Core 10.0.15, migrations au démarrage, service `db` dans compose (`docs/11`) ; (2) catégories relationnelles, filtres/pagination SQL, décrément de stock atomique (`docs/12`) ; (3) commandes persistées (instantanés, prix serveur, transaction, idempotence `Idempotency-Key`, emails Mailpit, `docs/13`) ; (4) comptes clients (JWT bearer `MapInboundClaims=false`, `UserId`=sub, `/orders/me`, 404 anti-énumération, RCL Account, `AuthorizeRouteView`, propagation jeton Web→API en circuit, cookie `__Host-` à `Path=/`, `client`/`client1234`, `docs/14`) ; (5) back-office (produit enrichi `Sku`/`OldPrice`/`Features` jsonb/`Id` identity, `/api/admin/*` sous `CanManageStock`, machine de statuts + emails, RCL Admin, `docs/15`) ; **refonte design e-commerce** (`docs/16`, fiche type joutech.ma : fil d'Ariane, promos, specs, liés, accueil vitrine, panier détaillé — axe 5/5) ; (6) **paiement Stripe** (`docs/17`) : `IPaymentGateway` interchangeable (Stripe.net 49.2.0 si `Stripe:SecretKey` configuré, sinon simulateur dev + `/fake-confirm` mappé seulement dans ce mode), Payment Intent idempotent (clé = orderId, montant depuis la commande persistée), **webhook signé = seule preuve** (Pending→Paid idempotent + email), Payment Element monté post-rendu + sondage jusqu'à Paid, CSP ouverte à Stripe. E2E 4/4, archi 14/14, axe 5/5. Chemin Stripe réel non vérifié faute de clés (commandes user-secrets dans `docs/17`). (7) **livraison + TVA + restock** (`docs/18`) : `StoreConfig` (port 5,90 EUR offert dès 60 EUR, TVA 20 % TTC) partagé API/Web via `/api/store/config`, `Order.ShippingCost/TaxRate` en instantanés, panier avec port réel + progression, restock atomique à l'annulation (même transaction, SignalR + cache), `DesignTimeDbContextFactory` (dotnet ef depuis Infrastructure), E2E à cibles configurables (`SHOPPULSE_APIURL`/`SHOPPULSE_WEBURL`), `scripts/dev.ps1`. (8) **observabilité** (`docs/19`) : `/health` JSON sur API (NpgSql + Keycloak) et Web (API), Serilog structuré (Application + TraceId, `using Serilog` rend `ILogger` ambigu — qualifier). (9) **remboursement + tests d'intégration + finitions** (`docs/20`) : `IPaymentGateway.RefundAsync` (idempotent, rembourser AVANT la transition), `tests/ShopPulse.Api.Tests` (WebApplicationFactory + Testcontainers, 16 tests — piège : le HttpClient de la factory route tout en mémoire, Keycloak exige un vrai HttpClient), tri catalogue SQL (`?sort=`), pagination admin (25/page), suivi invité signé HMAC temps constant (`/track/{id}?sig=`, lien dans l'email), i18n pages compte (22 clés × 3). (10) **refresh token + Outbox + sondes** (`docs/21`) : `UserTokenStore.GetValidTokenAsync` (refresh avec verrou + marge 60 s, vérifié realm à 60 s — preuve en base), pattern Outbox (`outbox_messages` écrit dans la transaction métier, `OutboxWorker` poll 2 s, retry ×5, reprise après panne SMTP démontrée), `/health/live` vs `/health`. E2E robustifiés (clic panier en boucle jusqu'au badge, Mailpit en sondage). (11) **CSP appliquée + i18n admin + métriques** (`docs/22`) : `Content-Security-Policy` bloquante (nonce sur JSON-LD, E2E/axe 5/5 en navigateur réel), back-office trilingue (40 clés × 3), OpenTelemetry métriques Prometheus sur `/metrics` (API + Web). Évolutions possibles : traces OTel vers collecteur, alerting, hashes CSP styles, sauvegardes PostgreSQL. Pile : `docker compose up -d` (5026/5299/8080/5432/8025) ou `dotnet run` (base via `docker compose up -d db mailpit`). Ce fichier documente l'architecture et les conventions du cours pour qu'un agent qui interviendrait ici connaisse le contexte.

## Contenu du répertoire

| Fichier | Nature |
|---|---|
| `ShopPulse_Cours_261117_141854.pdf` | Cours complet (100 pages) : les 8 modules, le code du fil rouge, les récapitulatifs et exercices. C'est la source de vérité principale. |
| `ShopPulse Architecture Universelle.pdf` | Diaporama de 19 planches (images uniquement, sans texte extractible) illustrant le module 8 : accessibilité, internationalisation et inclusion. |
| `frontend_engineering_160_concepts.md` | La liste des 8 domaines × 20 concepts du cours (titres en anglais, commentaires en français). |
| `src/` + `tests/` + `ShopPulse.slnx` | Solution modulaire créée au module 4 (monolithe modulaire) : Domain, SharedKernel, Application, Infrastructure, Modularity, UI (RCL design system), Features.Catalog/Cart/Checkout (RCL), Web (hôte), Api, Contracts, tests (NetArchTest + pluriels + axe + **E2E Playwright du tunnel de commande**). Voir `docs/04-architecture.md`. |
| `docker-compose.yml` + Dockerfiles | Bonus déploiement (chapitre `docs/09`) : **PostgreSQL (`db`)** + **Mailpit** (capture SMTP) + Keycloak + API + Web en conteneurs, DNS split-horizon résolu (`--hostname-backchannel-dynamic`). `docker compose up -d --build` pour toute la pile (ports 5026/5299/8080/5432/8025). |
| `docs/` | Documentation rédigée au fil des modules — un chapitre `.md` par étape/module, chaque concept abordé y est expliqué (matière première du livre). Format : `00` … `09` + `10-plan-de-manuscrit.md` + `11-boutique-reelle-*.md` (transformation en vraie boutique, par phase) + `manuscrit/` : `apropos-de-lauteur.md` (Issiaga Soumah), `chapitre-01.md` à `chapitre-10.md`, `conclusion.md` (**manuscrit complet**, calibré débutant). |
| `src/ShopPulse.Web/wwwroot/img/` | Photos produit réelles (p0-p9.jpg, 1024²) classées par catégorie ; `InMemoryProductService` génère noms descriptifs et `alt` (module 8). |
| `AGENTS.md` | Le présent fichier. |

## Pile technologique et architecture cible

- **Runtime cible : .NET 10 (`net10.0`), C# 14, Blazor Server** (Static SSR + `InteractiveServer` via circuit SignalR). Aucun C# ne s'exécute dans le navigateur.
- Le mode de rendu se choisit **par page ou par composant** (`@rendermode InteractiveServer`) ; ne jamais tout rendre interactif par défaut.
- Prérequis du cours : SDK .NET 10, Visual Studio / Rider / VS Code, Node.js (outils JS), Docker (optionnel : Keycloak, Redis), navigateur Chromium pour les tests.
- Les API .NET évoluent d'une version à l'autre : vérifier dans la documentation .NET 10 toute API dépendant de la version avant de s'y fier.

### Structure de solution visée (monolithe modulaire)

Le cours migre le projet initial vers une solution `ShopPulse.slnx` (format par défaut de .NET 10) organisée ainsi :

```
ShopPulse.slnx
Directory.Build.props        # net10.0, Nullable/ImplicitUsings enable,
                             # TreatWarningsAsErrors=true, LangVersion 14
Directory.Packages.props     # versions centralisées des paquets NuGet
src/
  ShopPulse.Domain/          # classlib : modèles, aucune référence ASP.NET
  ShopPulse.Application/
  ShopPulse.Infrastructure/
  ShopPulse.UI/              # Razor Class Library : design system
                             # (jetons CSS --sp-*, composants Atomic Design)
  ShopPulse.Features.Catalog/    # RCL : pages et composants du catalogue
  ShopPulse.Features.Cart/       # RCL : panier
  ShopPulse.Features.Checkout/   # RCL : tunnel de commande
  ShopPulse.Contracts/       # DTO partagés (records) — module 6
  ShopPulse.Api/             # Minimal API : /api/products, /api/stock,
                             # /hubs/stock (SignalR StockHub) — module 6
  ShopPulse.Web/             # hôte Blazor : assemble les modules via
                             # IShopModule + ModuleCatalog + AddAdditionalAssemblies
tests/
  ShopPulse.Architecture.Tests/  # règles de dépendances (NetArchTest.Rules)
  ShopPulse.A11y/                # tests axe (Playwright + Deque.AxeCore)
```

Garde-fous prévus par le cours : NetArchTest (ex. Domain ne référence jamais `Microsoft.AspNetCore`), CODEOWNERS, ADR pour les décisions structurantes.

Conventions de découpage : chaque feature est une **Razor Class Library** pour que le compilateur impose les frontières ; le design system `ShopPulse.UI` suit l'Atomic Design (`Components/Atoms`, `Molecules`, `Organisms`, `Templates`) ; les composants se divisent en présentation (« bêtes », ex. `ProductCard`), conteneurs (émettent des événements) et mise en page. Composition plutôt qu'héritage (`ChildContent`, `RenderFragment<T>`), `[EditorRequired]` pour les paramètres obligatoires, `[Parameter(CaptureUnmatchedValues = true)]` pour laisser passer `class` et `aria-*`.

## Commandes de build et d'exécution

Les commandes ci-dessous viennent du cours (à exécuter une fois le code créé) :

```bash
# Étape 0 — création du projet initial
dotnet new blazor -n ShopPulse -int Server -au None
cd ShopPulse && dotnet run

# Refonte en solution modulaire (module 4)
dotnet new sln -n ShopPulse
dotnet new classlib -n ShopPulse.Domain -o src/ShopPulse.Domain
dotnet new razorclasslib -n ShopPulse.UI -o src/ShopPulse.UI
dotnet new razorclasslib -n ShopPulse.Features.Catalog -o src/ShopPulse.Features.Catalog
dotnet sln add src/**/*.csproj
dotnet add src/ShopPulse.Features.Catalog reference src/ShopPulse.UI src/ShopPulse.Domain

# Publication (jamais en dotnet watch pour la production)
dotnet publish -c Release -o out
ASPNETCORE_ENVIRONMENT=Production dotnet out/ShopPulse.dll --urls https://localhost:5001

# Audit des dépendances
dotnet list package --vulnerable --include-transitive
```

## Stratégie de tests

Pyramide préconisée par le cours et le diaporama du module 8 :

- **Base (rapide, composants)** : tests C# unitaires avec **bUnit** et `A11yAssert` — absence d'ID dupliqués, présence d'attributs `alt`, pas de saut de niveau de titre.
- **Milieu (E2E / moteur WCAG)** : **Playwright + axe-core** (`Deque.AxeCore.Playwright`), matrice 3 langues × 2 thèmes, contrastes réels CSS et pièges à focus (modales). Navigateur Chromium requis.
- **Sommet (humain / manuel)** : navigation au clavier pure, lecteur d'écran (NVDA / VoiceOver), vérification du contraste forcé.
- **Architecture** : tests NetArchTest vérifiant les règles de dépendances entre projets.
- **i18n** : test CI qui échoue si une clé de traduction manque dans une langue ; pseudo-localisation (`Locale:Pseudo=true`) pour détecter les textes codés en dur.

Chaque module du cours se termine par des exercices de vérification manuelle (onglet Réseau, mode hors connexion, injection XSS, parcours clavier, etc.).

## Conventions du projet (à respecter dans tout ajout)

- Langue de la documentation et des textes d'interface : **français** (l'anglais et l'arabe sont ajoutés au module 8 via `IStringLocalizer` et des fichiers `.resx`).
- Marqueurs du cours : **« Piège »** signale une erreur fréquente, **« Limite »** ce que le code ne couvre volontairement pas. Les conserver si l'on ajoute de la documentation.
- Enregistrements de services : **Scoped par défaut** pour tout état utilisateur (panier, favoris, formulaires) ; un Singleton ne porte jamais d'état utilisateur. `CatalogNotifier` est le seul Singleton du module 3.
- État au plus bas niveau utile : filtres/page dans l'URL, liste produits dans un `QueryClient` (état serveur caché), formulaire dans `EditContext`, thème en `CascadingValue`.
- CSS : propriétés logiques (`margin-inline-start`, pas `margin-left`) pour le RTL ; jetons `--sp-*` du design system ; contraste WCAG 2.2 AA (texte 4,5:1, bordures/focus 3:1) dans les deux thèmes.
- ARIA en Razor : jamais d'attribut booléen nu (`aria-expanded="@_open"` génère un attribut sans valeur) — utiliser `aria-expanded="@(_open ? "true" : "false")"`.
- Interop JS / focus : les appels `FocusAsync` et l'interop JS attendent `OnAfterRenderAsync` ; ne jamais appeler `showModal()` pendant le prerender ; utiliser `@key` sur les nœuds focalisés pour éviter la perte de focus.
- Tout cache a une règle d'invalidation ; tout appel réseau a un délai, une reprise et une annulation ; ne jamais réessayer une écriture non idempotente.
- Secrets : `dotnet user-secrets` en développement, Key Vault en production, jamais de secret côté frontend ni dans le dépôt.

## Sécurité (module 7)

Principe directeur : **tout ce qui vient du navigateur est non fiable**, y compris dans Blazor Server. Blazor encode par défaut (`@valeur`) ; `MarkupString` n'est utilisé qu'avec nettoyage (`HtmlSanitizerService`). Defenses prévues : antiforgery + cookies `SameSite=Lax`, cookie `__Host-` `HttpOnly` `Secure` avec jetons côté serveur, OIDC avec PKCE (Keycloak en dev), CSP avec nonce (d'abord Report-Only), `frame-ancestors 'none'`, HTTPS + HSTS, validation du `returnUrl` (open redirect), expiration de session (inactivité + absolue), NuGetAudit/Dependabot/SBOM, RateLimiter et CORS strict sur l'API.

## Accessibilité et internationalisation (module 8)

Objectif : WCAG 2.2 niveau AA, locales `/fr/...`, `/en/...`, `/ar/...` avec hreflang et routage par middleware `LocalePathMiddleware` (cookie `sp_locale`, header `Accept-Language` ; ne jamais rediriger `/api` ni `/_blazor`). HTML sémantique d'abord (header/nav/main/footer, `<dialog>` natif, skip links via `a11y.js` qui intercepte `[data-sp-skip]` car `<base href="/">` casse les ancres), `LiveAnnouncer` (`aria-live="polite"` / `"assertive"` injectés dans `App.razor`), pluriels CLDR (6 formes en arabe via `PluralRules.cs`), formats par `Culture` (devise de la boutique en `StoreCulture`, invariant pour URL/JSON/logs), `<bdi>` pour isoler le texte latin en arabe. Le client HTTP doit transmettre manuellement `Accept-Language` à l'API.

Les « 5 règles d'or » du bilan du cours (diaporama, planche 19) : (1) commencer statique, rendre interactif sur besoin ; (2) tout cache est une promesse d'invalidation ; (3) le navigateur (DOM, cookies, focus) n'est jamais digne de confiance ; (4) ce qui n'est pas testé automatiquement régresse ; (5) une seule source de vérité par donnée.

## Points de vigilance pour un agent

- Ne pas inventer de structure de code : le code de `ShopPulse/` suit le cours PDF, étape par étape. Ne pas ajouter de fonctionnalité en avance sur le module en cours.
- Règle de rédaction : après chaque étape/module terminé et vérifié (build + exécution OK), rédiger ou mettre à jour le chapitre correspondant dans `docs/`, en français, avec une section par concept abordé, les marqueurs « Piège » et « Limite », et un bilan des notions.
- Les deux PDF ne sont pas indexables par des outils texte classiques : le cours est extractible par `pdftotext`, le diaporama « Architecture Universelle » est composé d'images (il faut le visualiser page par page).
- Les numéros de version de paquets donnés dans le cours (ex. xunit, NetArchTest.Rules) sont des exemples : prendre les dernières versions stables au moment de l'installation.
- Les paquets `Microsoft.AspNetCore.*` et `Microsoft.Extensions.*` doivent rester alignés sur la **même version majeure que le runtime** installé (un `dotnet add package` sans version peut résoudre une majeure supérieure → `MissingMethodException` qui tue les circuits Blazor en silence). Vérifier avec `dotnet --list-runtimes` et épingler dans `Directory.Packages.props`.
