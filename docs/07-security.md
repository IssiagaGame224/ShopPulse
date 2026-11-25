# Chapitre 7 — Module 7 : Security (la sécurité)

> **Question du module :** comment empêcher qu'un utilisateur, un script tiers ou un attaquant détourne l'application ?
> **Principe directeur : tout ce qui vient du navigateur est non fiable**, y compris dans Blazor Server où le C# s'exécute chez vous. Les événements envoyés par le circuit sont des entrées utilisateur comme les autres (règle d'or n° 3).
> **Fil rouge :** OIDC avec Keycloak, en-têtes de sécurité + CSP à nonce, sanitisation HTML, expiration de session, page d'administration protégée.

## État du fil rouge après le module

| Menace | Défense en place |
|---|---|
| XSS | Encodage Blazor, `HtmlSanitizerService`, CSP avec nonce (Report-Only) |
| CSRF | Antiforgery, cookies `SameSite=Lax` |
| Vol de session | Cookie `__Host-`, `HttpOnly`, `Secure`, jetons côté serveur (`SaveTokens`) |
| Clickjacking | `frame-ancestors 'none'` (CSP) + `X-Frame-Options: DENY` |
| Interception réseau | HTTPS partout + HSTS (production), `RequireHttpsMetadata` exigé hors dev |
| Dépendances | NuGetAudit en erreur de build (NU1901-1904), versions centralisées |
| Fuite de secrets | `dotnet user-secrets` (dev), DTO publics sans champ sensible |
| Session abandonnée | Expiration d'inactivité (20 min glissante) + absolue (8 h), revalidation du circuit |
| Redirection ouverte | Validation de `returnUrl` (`LocalUrl`) |

## 1. XSS (Cross-Site Scripting)

L'attaquant fait exécuter son script dans le navigateur d'une victime. **Blazor encode par défaut** tout ce qui est écrit avec `@valeur` — `<script>` s'affiche littéralement. Les points à surveiller :

| Danger | Pourquoi | Remède |
|---|---|---|
| `MarkupString` | HTML brut, exécution possible | Nettoyer avant (concept 10), ou ne pas l'utiliser |
| `JS.InvokeVoidAsync("eval", …)` / `innerHTML` | Exécution de chaînes | Appeler des fonctions nommées avec des paramètres |
| Attributs `href/src` utilisateur | Contexte non encodé | Valider le schéma (`https` seulement) |
| Concaténation dans un `<script>` | — | Passer par l'interop, pas par la chaîne |

```csharp
static string SafeUrl(string? url) =>
       Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "https" or "http"
              ? u.AbsoluteUri : "#";
```

La CSP (concept 3) est la deuxième ligne de défense si une faille passe.

## 2. CSRF (Cross-Site Request Forgery)

Un site malveillant déclenche, depuis le navigateur de la victime, une requête vers votre site en profitant de ses cookies. Remèdes : **jeton antiforgery** (`app.UseAntiforgery()`, déjà en place depuis l'Étape 0) et cookies `SameSite`. Dans Blazor, `EditForm` ajoute le jeton automatiquement (rendu statique) ; pour un `<form>` HTML pur, `<AntiforgeryToken />` à la main. Les interactions via le circuit SignalR ne sont pas des requêtes de formulaire : la connexion WebSocket n'est pas forgeable de la même manière, et l'origine est vérifiée par le serveur — ne désactivez pas cette vérification.

## 3. Content Security Policy

Un en-tête qui liste les sources autorisées de scripts, styles, images, connexions. Une injection de script est alors **bloquée par le navigateur**. Le fil rouge (`ShopPulse.Web/Services/SecurityHeaders.cs`) : un **nonce par requête**, stocké dans `HttpContext.Items`, porté par les scripts en ligne de `App.razor`, et posé dans l'en-tête :

```csharp
ctx.Response.Headers["Content-Security-Policy-Report-Only"] =
       "default-src 'self'; " +
       $"script-src 'self' 'nonce-{nonce}'; " +
       "style-src 'self' 'unsafe-inline'; " +     // bootstrap/styles inline du modèle ; à resserrer
       "img-src 'self' data:; " +
       "connect-src 'self' ws: wss:; " +          // ws:/wss: = circuit SignalR (dev en clair)
       "frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";
```

Points propres à Blazor : `blazor.web.js` est servi par votre origine (`script-src 'self'` suffit) ; les scripts en ligne doivent porter `nonce="@cspNonce"` (Razor omet l'attribut quand la valeur est `null` — attribut conditionnel) ; `connect-src` doit autoriser la WebSocket du circuit, sinon l'application ne s'active pas.

**Vérifiés** : les 6 en-têtes de sécurité présents sur toutes les réponses ; le nonce de l'en-tête correspond au nonce du `<script>` (aprient décodage HTML des `+`) ; l'en-tête `Server` masqué (`ConfigureKestrel(k => k.AddServerHeader = false)`).

> **Observations vécues** : (1) .NET 10 ajoute lui-même un en-tête `Content-Security-Policy: frame-ancestors 'self'` sur les réponses Blazor — deux en-têtes CSP se combinent par intersection, donc notre future CSP bloquante gardera le sens voulu ; (2) en Development, `MapStaticAssets` servait l'ancien `App.razor` en `no-cache` — visible immédiatement, pas de piège ici.

> **Exercice 1** : laisser tourner en Report-Only, relever les violations dans la console (elles sont journalisées, pas bloquées), resserrer `style-src`, puis renommer l'en-tête en `Content-Security-Policy`.

## 4. CORS (côté sécurité)

Règle du navigateur (rappel module 6). Pour la sécurité : liste blanche explicite jamais `*` avec des cookies ; méthodes et en-têtes minimaux ; CORS ne remplace pas l'authentification ; les préliminaires `OPTIONS` ne doivent pas exiger d'authentification. Avec Blazor Server en BFF, CORS ne concerne pas les appels navigateur → serveur → API.

## 5. Clickjacking

Un site malveillant affiche votre page dans un `<iframe>` invisible et fait cliquer la victime. Remède : `frame-ancestors 'none'` (CSP, méthode actuelle) + `X-Frame-Options: DENY` (anciens navigateurs). Les deux sont posés par le middleware du fil rouge.

## 6. Secure Cookies

Attributs essentiels d'un cookie d'authentification :

| Attribut | Effet |
|---|---|
| `HttpOnly` | Illisible par JavaScript (limite le vol par XSS) |
| `Secure` | Envoyé en HTTPS seulement |
| `SameSite=Lax` | Non envoyé dans la plupart des requêtes inter-sites (limite CSRF) |
| Préfixe `__Host-` | Lié à l'hôte exact, Secure, `Path=/`, sans `Domain` |
| `Max-Age` courts | Limite la fenêtre d'exploitation |

Le fil rouge : `o.Cookie.Name = "__Host-shoppulse"`, `HttpOnly`, `SecurePolicy.Always`, `SameSite=Lax`, expiration 20 minutes glissante.

> **Piège** : `SameSite=Strict` casserait le retour de la redirection OIDC (cookie non envoyé lors de cette navigation inter-sites depuis le fournisseur) : **Lax est le bon compromis pour l'authentification**.

## 7. Token Storage

| Emplacement | Lisible par XSS ? | Verdict |
|---|---|---|
| localStorage / sessionStorage | Oui | À éviter |
| Variable JS en mémoire | Oui (pendant l'exécution) | Mieux, mais perdue au rechargement |
| Cookie HttpOnly | Non | Bon |
| **Côté serveur (Blazor Server)** | **Non (jamais envoyé au navigateur)** | **Meilleur** |

Le fil rouge : `SaveTokens = true` — les jetons sont dans le cookie d'authentification **chiffré**, jamais exposés au JS ; côté serveur, `await httpContext.GetTokenAsync("access_token")`.

> **Piège** : dans un composant interactif, `HttpContext` n'est pas disponible de manière fiable ; capturez les jetons au démarrage du circuit ou passez par un service de cache par utilisateur.

## 8. OAuth with PKCE

Authorization Code + **PKCE** : le flux recommandé pour tous les clients. PKCE ajoute un secret à usage unique (le code verifier) dont seul le hash (challenge) est envoyé au départ — un code d'autorisation volé est inutilisable. ASP.NET Core le gère pour vous (`o.UsePkce = true`). Un client confidentiel (Blazor Server, secret gardé côté serveur) utilise quand même PKCE : c'est la recommandation actuelle. N'utilisez plus le flux implicit.

Le fil rouge (Program.cs) : `ResponseType = Code`, `SaveTokens`, `GetClaimsFromUserInfoEndpoint = true`, `MapInboundClaims = false`, scopes `openid profile email`, et le mapping des rôles realm Keycloak.

> **Piège vécu (mapping des rôles)** : le realm importé était correct (utilisateur `demo` avec le rôle `stock-manager`, mapper « realm-roles » exportant une claim `role` en tableau), et la connexion réussissait — mais `/admin/stock` renvoyait **AccessDenied**. Diagnostic via un endpoint `/whoami` temporaire : les revendications `role = stock-manager` arrivaient bien, sous leur **nom brut** (`MapInboundClaims = false` désactive le rebasculement vers `ClaimTypes.Role`), et `MapUniqueJsonKey` avait produit une seule claim contenant le tableau sérialisé en chaîne. Correctif : `TokenValidationParameters.RoleClaimType = "role"` et `NameClaimType = "preferred_username"`. **Vérifié de bout en bout** : `/login?returnUrl=/admin/stock` → Keycloak → connexion `demo` → retour sur la page protégée, « Bonjour demo » affiché, cookie `__Host-shoppulse` avec `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`.

> **Épisode d'infrastructure vécu** : Keycloak en conteneur exigeait WSL2, absent de la machine (service `LxssManager` inexistant, Docker Desktop sans moteur Linux). `wsl --update` (composant de magasin, sans élévation) a installé WSL 3.0.5 ; un redémarrage de Docker Desktop a ramené le moteur, et le realm s'est importé du premier coup.

## 9. JWT Handling

Un JWT est **signé, pas chiffré** : tout le monde peut lire son contenu — ne mettez rien de secret dedans. Validez toujours émetteur, audience, expiration, signature, algorithme (`ValidAlgorithms = [RsaSha256]` refuse « none » et les algorithmes inattendus), `ClockSkew` court. Durée de vie courte (5-15 min) + rafraîchissement côté serveur. Un JWT ne se révoque pas : durées très courtes ou liste noire. Ne jamais décider de sécurité en lisant un JWT « à la main » sans valider. (La validation JWT concerne l'API consommée par d'autres clients ; le fil rouge BFF utilise le cookie de session.)

## 10. Input Sanitization

Distinguer : **valider** (refuser ce qui n'est pas attendu), **encoder** (selon le contexte de sortie), **nettoyer** (retirer le dangereux d'un HTML qu'on veut afficher). Le fil rouge : `HtmlSanitizerService` (paquet NuGet **HtmlSanitizer**, API `Ganss.Xss`) avec une liste blanche stricte : tags `p b i em strong ul ol li a br`, attribut `href` seul, schéma `https` seul.

```razor
@* MarkupString n'est utilisé qu'avec le nettoyage : *@
@((MarkupString)Sanitizer.Clean(commentaireHtml))
```

Validation côté serveur **quel que soit ce que fait l'interface**. SQL toujours paramétré ; chemins de fichiers validés (`Path.GetFileName`, confinement au dossier autorisé).

## 11. Dependency Audits

Votre code embarque des centaines de paquets : chacun est une surface d'attaque. `dotnet list package --vulnerable --include-transitive`. Le fil rouge verrouille la CI (`Directory.Build.props`) :

```xml
<NuGetAudit>true</NuGetAudit>
<NuGetAuditMode>all</NuGetAuditMode>
<NuGetAuditLevel>moderate</NuGetAuditLevel>
<WarningsAsErrors>$(WarningsAsErrors);NU1901;NU1902;NU1903;NU1904</WarningsAsErrors>
```

**Vécu deux fois au module 6** : `Microsoft.OpenApi 2.0.x` puis `SignalR 10.0.2` signalés vulnérables — la build a échoué (NU1903) et la gestion centralisée des versions a rendu chaque correctif trivial. Compléter avec : `packages.lock.json`, Dependabot/Renovate, sources NuGet restreintes, SBOM en CI, revue avant toute nouvelle dépendance.

## 12. Subresource Integrity

SRI vérifie qu'un fichier chargé depuis un tiers (CDN) n'a pas été modifié : le navigateur compare son hash à celui indiqué (`integrity="sha384-…" crossorigin="anonymous"`). Règles : versions exactes (jamais `@latest`), `crossorigin`, et préférence pour l'auto-hébergement. Les fichiers de votre propre origine, empreints par `MapStaticAssets` (module 5), n'ont pas besoin de SRI.

## 13. HTTPS Everywhere

Tout le trafic doit être chiffré : sinon mots de passe, cookies et contenus peuvent être lus ou modifiés en route. HSTS dit au navigateur de ne plus jamais utiliser HTTP pour le domaine (`MaxAge` 365 j, `IncludeSubDomains` ; commencer court ; `Preload` en connaissance de cause). Les ressources mixtes sont bloquées ; `upgrade-insecure-requests` aide. Derrière un proxy qui termine TLS, configurer les en-têtes transférés **en acceptant seulement le proxy connu** (sinon n'importe qui usurpe son IP) : en .NET 10, `KnownNetworks` est remplacé par **`KnownIPNetworks`** (vérifier le nom dans votre version — piège vérifiable).

Le fil rouge : `RequireHttpsMetadata = !IsDevelopment()` — Keycloak `start-dev` local est en HTTP, mais la métadonnée OIDC exige HTTPS par défaut hors développement (piège vécu : sans ce réglage, **chaque requête** plantait dans `UseAuthentication`).

## 14. iframe Sandboxing

Pour inclure du contenu non fiable, `sandbox` retire presque tous ses droits : sans jeton, presque rien ; `allow-scripts`, `allow-forms`, `allow-same-origin` au cas par cas.

> **Danger** : `allow-scripts` + `allow-same-origin` sur du contenu de votre propre origine **annule la protection** (le contenu peut retirer lui-même l'attribut). Côté CSP, `frame-src` limite les sources inclusibles ; un `title` est requis pour l'accessibilité.

## 15. Permissions Policy

En-tête qui désactive les fonctionnalités du navigateur dont vous n'avez pas besoin, y compris pour les iframes et scripts tiers : `camera=(), microphone=(), geolocation=()` — même un script malveillant ne peut pas demander l'accès à la caméra. Posé par le middleware du fil rouge.

## 16. No Secrets in Frontend

Tout ce qui est envoyé au navigateur est public. En Blazor Server, le C# reste sur le serveur, mais pas ce que vous mettez dans le rendu ou le `PersistentComponentState`. DTO dédiés sans champ sensible (`ProductPublicView`, jamais `CostPrice`/`SupplierApiKey`). Secrets : `dotnet user-secrets` en développement (le fil rouge : `Oidc:ClientSecret` dans le magasin utilisateur, jamais dans le dépôt), variables d'environnement/Key Vault en production, Data Protection avec magasin partagé et chiffrement au repos pour les clés. Un secret divulgué doit être **révoqué**, pas seulement retiré du dépôt.

## 17. Auth Flows

Vue d'ensemble : OIDC (code + PKCE), cookie de session, autorisation par politiques. Le fil rouge :

```csharp
builder.Services.AddAuthorization(o =>
{
       o.AddPolicy("CanManageStock", p => p.RequireAuthenticatedUser().RequireRole("stock-manager"));
       o.FallbackPolicy = null;   // le catalogue reste public
});
builder.Services.AddCascadingAuthenticationState();
```

Pages : `@attribute [Authorize(Policy = "CanManageStock")]` sur `/admin/stock` (Features.Catalog), `AuthorizeView` pour l'UI.

```csharp
app.MapGet("/login", (string? returnUrl) =>
       Results.Challenge(new AuthenticationProperties { RedirectUri = LocalUrl(returnUrl) },
                         [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapPost("/logout", () => Results.SignOut(...)).RequireAuthorization();

static string LocalUrl(string? url) =>   // anti-redirection ouverte
       !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") ? url : "/";
```

Points de sécurité : `AuthorizeView` cache l'interface mais ne protège pas — l'autorisation doit se faire aussi dans les services et l'API ; en Blazor interactif, l'identité est celle du début du circuit (revalider périodiquement, concept 18) ; valider toujours `returnUrl` ; verrouiller par défaut (`FallbackPolicy` exigeant l'authentification + `[AllowAnonymous]` sur les pages publiques) est plus sûr que l'inverse.

## 18. Session Timeout

Une session ne doit pas durer indéfiniment. Deux protections : **expiration d'inactivité** (20 min, glissante) et **expiration absolue** (8 h) via `OnValidatePrincipal` — les deux sur le cookie du fil rouge. Dans un circuit interactif, la requête HTTP d'authentification n'est plus rejouée : un `AuthenticationStateProvider` personnalisé (`RevalidatingProvider`, revalidation toutes les 5 min) reprend le flambeau. Côté interface, prévenez l'utilisateur avant l'expiration (message accessible, possibilité de prolonger — critère WCAG 2.2.1, module 8), sinon il perd son travail.

## 19. Security Headers

Le tableau récapitulatif (tous vérifiés dans le fil rouge) : `Strict-Transport-Security` (production), CSP (concept 3), `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`, `Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Resource-Policy: same-origin`, `X-Frame-Options: DENY`. Masquer l'en-tête `Server`. Tester avec un analyseur d'en-têtes en ligne ou en CI.

## 20. Third Party Scripts

Chaque script tiers s'exécute avec tous les droits de votre page : première source de fuite réelle. Mesures, de la plus forte à la plus faible : (1) ne pas en mettre, ou solutions côté serveur ; (2) auto-héberger une version figée ; (3) iframe avec `sandbox` ; (4) CSP stricte (`script-src` précis, `connect-src` limité) ; (5) SRI ; (6) chargement conditionnel après consentement, jamais sur paiement/connexion. Maintenir un inventaire des scripts tiers (qui, pourquoi, quelles données, un propriétaire).

---

## Les trois choses à retenir

1. **La défense est en couches, et chaque couche est cheap** : encodage par défaut + CSP + sanitisation pour XSS ; antiforgery + SameSite pour CSRF ; `__Host-` + HttpOnly + jetons serveur pour la session. Aucune seule suffit, toutes se combinent.
2. **Blazor Server est un excellent modèle de sécurité** : le code et les jetons ne quittent jamais le serveur, le navigateur ne voit que du HTML — mais tout ce qui revient du navigateur reste une entrée hostile.
3. **La build est un garde-fou de sécurité** : NuGetAudit en erreur, versions centralisées alignées sur le runtime — les vulnérabilités connues doivent faire échouer la CI, pas alimenter un tableau de bord ignoré.

## Exercices du module

1. CSP en Report-Only → relever les violations → passer en bloquante (renommer l'en-tête dans `SecurityHeaders.cs`).
2. Injection `<img src=x onerror=alert(1)>` : encodé par `@valeur`, neutralisé ; avec `MarkupString` seul, exécuté ; avec `MarkupString` + `Sanitizer.Clean`, neutralisé.
3. Après connexion : vérifier que le cookie `__Host-shoppulse` est `HttpOnly`, `Secure`, préfixé `__Host-` (onglet Application).
4. `/login?returnUrl=https://evil.example` → la validation `LocalUrl` renvoie vers `/`.
5. `dotnet list package --vulnerable --include-transitive` (le build l'échoue déjà, par conception).
6. Passer `ExpireTimeSpan` à 1 minute : observer la déconnexion après inactivité.
