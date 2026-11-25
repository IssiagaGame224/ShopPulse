# 14 — Vraie boutique, phase 4 : les comptes clients

> **Contexte.** Phase 4 : les commandes se lient au compte du client (claim `sub`
> Keycloak), les pages « Mon compte » affichent profil et historique, et
> `GET /api/orders/{id}` applique un contrôle d'accès. Cette phase a mis au jour
> **trois pièges réels** du couple Blazor Server + OIDC, documentés ci-dessous.

## L'API valide les jetons Keycloak (JWT bearer)

`Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.15, aligné runtime) :

```csharp
o.Authority = config["Oidc:Authority"];                  // métadonnées + JWKS
o.TokenValidationParameters.ValidateAudience = false;    // Limite dev : pas d'audience dédiée
o.MapInboundClaims = false;                              // ← piège n° 1
o.TokenValidationParameters.NameClaimType = "preferred_username";
o.TokenValidationParameters.RoleClaimType = "role";
```

**Piège n° 1 — `MapInboundClaims`.** Par défaut, JwtBearer **renomme** les claims
(`sub` → `ClaimTypes.NameIdentifier`, `role` → `ClaimTypes.Role`) : `FindFirst("sub")`
ne trouve plus rien. Symptôme observé : commandes créées avec `UserId` NULL malgré un
jeton valide, et `/orders/me` en 401. Le Web (OIDC) avait déjà `MapInboundClaims=false`
(module 7) ; l'API a besoin du même réglage.

Les endpoints restent **anonymes par défaut** (catalogue, checkout invité) ;
`.RequireAuthorization()` n'est posé que sur `/orders/me`.

## Liaison commande ↔ compte : le `sub` vient du jeton, jamais du client

- `POST /api/orders` : si un jeton valide accompagne la requête, l'API lit
  `ctx.User.FindFirst("sub")` et le stocke dans `Order.UserId`. Le corps de la requête
  ne transporte **jamais** d'identifiant utilisateur — impossible à falsifier.
- `GET /api/orders/me` : historique du client (Guid v7 triés desc = plus récentes d'abord).
- `GET /api/orders/{id}` : commande liée → réservée à **son** compte ; **404** (pas 403)
  pour tout autre appelant — ne pas révéler l'existence d'une commande. Commande invitée
  (`UserId` nul) → lisible par lien (Guid non devinable).

**Limite.** Le suivi invité durci (lien signé id + email) et le rattachement des
commandes invitées à un compte créé ensuite (par email vérifié) sont des évolutions.

## Propager le jeton du Web vers l'API — pièges n° 2 et n° 3

Le Web détient les jetons (cookie chiffré, `SaveTokens`, module 7). Les appels API
passent par un `DelegatingHandler` (`AccessTokenHandler`) sur le client nommé « api ».
Deux contextes d'exécution très différents :

**SSR (pages statiques)** : `IHttpContextAccessor.HttpContext.GetTokenAsync("access_token")`
fonctionne — le jeton est relu du cookie.

**Circuit Blazor (clics)** : `HttpContext` est **null**. Et
`AuthenticationStateProvider.GetAuthenticationStateAsync()` lève
`InvalidOperationException` hors du scope d'un composant — un `DelegatingHandler`
n'est pas un composant (**piège n° 3**). Solution en deux pièces :

1. **`UserTokenStore`** (singleton, `IMemoryCache`) : à la connexion
   (`OnTokenValidated` OIDC), le jeton d'accès est conservé par `sub` pour sa durée de
   validité (`ExpiresIn`, 30 min en dev).
2. **`CurrentUserAccessor`** (scoped) : `MainLayout` reçoit l'`AuthenticationState` **en
   cascade** (autorisé dans un composant, y compris en circuit) et y dépose le `sub`.
   Le handler lit `sub` → jeton dans le store.

**Limite.** Pas de *refresh token* : passée la demi-heure, les appels API repartent en
anonyme jusqu'à la prochaine connexion (le cookie de session, lui, glisse sur 20 min).
Le flux de rafraîchissement est l'évolution naturelle.

## Piège n° 2 — le cookie `__Host-` et le PathBase : boucle de connexion infinie

Le plus vicieux. Symptôme : après un login réussi, re-challenge OIDC en boucle infinie.
Diagnostic (reproduction curl complète du flux : challenge → login Keycloak → form_post
→ callback) : le callback `/fr/signin-oidc` **réussit** (302 + cookie posé), mais la
requête suivante repart en challenge. Cause, visible dans les en-têtes :

```
Set-Cookie: __Host-shoppulse=…; path=/fr; secure; …
```

Le builder de cookie d'authentification reprend par défaut le **PathBase** de la requête
— ici `/fr`, posé par le middleware de locale (module 8) puisque le challenge part d'une
page préfixée (`[Authorize]` sur `/fr/account`). Or la règle **`__Host-` exige `Path=/`** :
le navigateur **rejette silencieusement** le cookie de session → utilisateur jamais
connecté → nouveau challenge → boucle.

Correction (Web `Program.cs`) : `o.Cookie.Path = "/";` — explicite, conforme `__Host-`,
valable quel que soit le préfixe de locale.

**Leçon.** Ce bug existait depuis le module 8 : il ne se manifestait que si le challenge
partait d'une URL préfixée. Les pages `[Authorize]` (compte, back-office) l'ont révélé.
Un test E2E de connexion l'aurait attrapé dès le module 7 — d'où le test ci-dessous.

## Les pages « Mon compte » (feature RCL `ShopPulse.Features.Account`)

- `/account` : profil lu depuis les claims (nom, email) + déconnexion ;
- `/account/orders` : historique (référence courte, date, total, statut en clair) ;
- `/account/orders/{id}` : détail lignes + livraison.
- `[Authorize]` appliqué au **routage** : `Routes.razor` passe de `RouteView` à
  **`AuthorizeRouteView`** (le `[Authorize]` de `AdminStock`, module 7, devient ainsi
  réellement appliqué) ; non connecté → `RedirectToLogin` → `/login?returnUrl=…`
  (returnUrl validé, module 7).
- NavMenu : « Mon compte » / « Se connecter » selon l'état (`AuthorizeView`).
- Pages **statiques (SSR)** : aucun état interactif, le HTML arrive complet.

**Limite.** Textes en français codés en dur (comme `Catalog.razor`) ; la localisation
des pages compte (clés resx fr/en/ar) reste à faire.

## Realm de développement enrichi

`docker/keycloak/realm-shoppulse.json` : utilisatrice **`client` / `client1234`** (sans
rôle back-office), `accessTokenLifespan: 1800` (30 min, aligné sur l'expérience dev),
`directAccessGrantsEnabled` (obtenir un jeton par curl pour tester l'API).
Rechargé par `docker compose up -d --force-recreate keycloak`.

## Vérifications effectuées

- Build solution 0/0 ; architecture **14/14** ;
- API (jetons obtenus par grant direct) : commande liée au `sub` ✓, `/orders/me` → 1
  commande ✓, sans jeton → **401** ✓, commande liée lue anonymement → **404** ✓, par un
  autre utilisateur → **404** ✓, par son propriétaire → **200** ✓, commande invitée
  lisible anonymement → **200** ✓ ;
- **E2E Playwright authentifié** (`AccountE2eTests`) : page protégée → Keycloak → login
  `client` → achat complet → « Mes commandes » contient la commande → API anonyme → 404.
  Le test invité de la phase 3 passe toujours (régression couverte) ;
- anecdotique mais formateur : une itération du test « échouait » parce que le produit
  choisi (id 7) fait partie des produits seedés **à stock zéro** — l'API répondait 409,
  exactement comme prévu. Le test utilise désormais le produit 9.

## Bilan des notions

- `MapInboundClaims=false` partout où l'on lit des claims bruts (`sub`, `role`) ;
- `__Host-` ⇔ `Path=/` : le PathBase du middleware de locale ne doit jamais fuiter dans
  les cookies — un E2E de login est le seul filet fiable ;
- circuit Blazor ≠ requête HTTP : ni `HttpContext` ni `AuthenticationStateProvider` dans
  un handler — cascade + store par `sub` ;
- 404 plutôt que 403 pour les ressources d'autrui ; identité jamais fournie par le client ;
- les pages de consultation restent statiques : l'interactivité n'est pas un réflexe.

**Prochaine phase** : back-office (commandes, statuts, gestion produits — la politique
`CanManageStock` et le rôle `stock-manager` sont déjà en place).
