# Chapitre 5 — Module 5 : Caching (le cache)

> **Question du module :** comment ne pas refaire deux fois le même travail, et comment continuer à fonctionner sans réseau ?
> **Principe directeur :** le cache est présent à chaque étage (navigateur, CDN, serveur, appareil), et chaque étage ajoute un risque — servir une donnée périmée. **Tout cache est une promesse d'invalidation** (règle d'or n° 2).
> **Fil rouge :** service worker avec app shell et page hors ligne, IndexedDB, clés de cache versionnées, SWR côté serveur.

## Les étages du cache dans ShopPulse

```
Navigateur     (HTTP cache, Service Worker, IndexedDB, localStorage)
CDN / proxy    (Cache-Control public, s-maxage)
Serveur ASP.NET (OutputCache, HybridCache, IMemoryCache)
Base de données / API
```

## 1. Browser Cache

Le navigateur garde les réponses selon les en-têtes `Cache-Control` :

| Directive | Effet |
|---|---|
| `max-age=N` | Réutilisable N secondes sans demander au serveur |
| `no-cache` | Réutilisable après revalidation (ETag) |
| `no-store` | Ne jamais stocker (données sensibles) |
| `public` / `private` | Partageable par un CDN / réservé au navigateur |
| `immutable` | Ne changera jamais pendant max-age |
| `stale-while-revalidate=N` | Servir le périmé N secondes pendant le rafraîchissement |

Les fichiers statiques : .NET 10 fournit `MapStaticAssets` (fichiers empreints, en-têtes adaptés). Les **pages avec données personnelles doivent porter `Cache-Control: no-store`** (ou `private`), sinon un cache partagé pourrait les servir à quelqu'un d'autre. Le fil rouge l'applique dans le middleware Server-Timing : `if (ctx.User.Identity?.IsAuthenticated == true) → no-store`.

## 2. Service Workers

Un Service Worker est un script JavaScript qui s'exécute en arrière-plan, **entre la page et le réseau**. Il intercepte les requêtes (`fetch`) et décide : cache, réseau ou les deux. Il exige HTTPS (sauf localhost).

```javascript
// wwwroot/service-worker.js (fil rouge)
self.addEventListener("install", (event) => {
   event.waitUntil(
       caches.open(STATIC_CACHE).then((cache) => cache.addAll(PRECACHE)).then(() => self.skipWaiting())
   );
});
```

Enregistrement depuis `App.razor` (script classique, hors circuit) :

```html
<script>
   if ("serviceWorker" in navigator) {
       navigator.serviceWorker.register("/service-worker.js");
   }
</script>
```

> **Piège spécifique à Blazor Server** : le circuit utilise une connexion SignalR (WebSocket). **Ne mettez jamais `/_blazor` en cache** et laissez passer toutes les requêtes non-GET (POST : formulaires, antiforgery). Le service worker ne fait pas fonctionner Blazor Server hors ligne : sans réseau, plus de circuit. Il sert en revanche les pages statiques déjà vues et une page « hors ligne ».

## 3. Cache API

L'API `caches` du navigateur, utilisée par le service worker pour stocker des paires Request→Response :

```javascript
async function cacheFirst(request) {
   const cache = await caches.open(STATIC_CACHE);
   const hit = await cache.match(request);
   if (hit) return hit;

   const response = await fetch(request);   // clone : un corps ne se lit qu'une fois
   if (response.ok) cache.put(request, response.clone());
   return response;
}
```

| Stratégie | Principe | Pour |
|---|---|---|
| **Cache first** | Cache, sinon réseau | Ressources versionnées (CSS, JS, images) |
| **Network first** | Réseau, sinon cache | Pages, données qui changent |
| **Stale-while-revalidate** | Cache tout de suite, rafraîchit derrière | Listes, avatars |
| **Network only** | Toujours réseau | Paiement, authentification |
| **Cache only** | Seulement cache | Ressources précachées |

Le service worker du fil rouge les applique par type de requête : navigation → network first avec repli ; ressources empreintes → cache first ; le reste → SWR.

## 4. IndexedDB

Base de données clé-valeur transactionnelle du navigateur, pour de gros volumes de données structurées — utile pour les données hors ligne et la **file d'attente** (concept 16 du plan / exercice 4). Utilisée depuis JavaScript (`wwwroot/js/db.js` du fil rouge) ; Blazor Server y accède via l'interop avec un module ES, wrapper C# `BrowserDb` dans `ShopPulse.Infrastructure` :

```csharp
public sealed class BrowserDb(IJSRuntime js) : IAsyncDisposable
{
       public async Task PutAsync<T>(string store, T value) =>
              await (await Mod()).InvokeVoidAsync("put", store, value);

       public async Task<T[]> GetAllAsync<T>(string store) =>
              await (await Mod()).InvokeAsync<T[]>("getAll", store);
}
```

## 5. LocalStorage

Stockage clé-valeur synchrone, chaînes uniquement, ~5 Mo, persistant, partagé entre onglets de la même origine. À éviter pour les gros volumes (il bloque le fil principal). En Blazor Server : `ProtectedLocalStorage` (chiffré, module 3) pour les préférences.

> **Piège sécurité** : jamais de jeton d'accès ni de secret en localStorage — n'importe quel script injecté (XSS) peut le lire (module 7).

## 6. SessionStorage

Comme localStorage, mais limité à l'onglet et effacé à sa fermeture : brouillons temporaires, état d'un assistant multi-étapes (`ProtectedSessionStorage`). À appeler après le premier rendu (`OnAfterRenderAsync`) — le préréndu n'a pas de navigateur.

## 7. Stale While Revalidate

Servir immédiatement la copie en cache (même un peu ancienne), et lancer en parallèle une requête pour la rafraîchir. L'utilisateur voit tout de suite quelque chose ; la prochaine visite est à jour. Trois implémentations :

- **Navigateur** (service worker) : `staleWhileRevalidate(event)` dans le fil rouge.
- **HTTP** : `Cache-Control: max-age=60, stale-while-revalidate=300` délègue la logique au navigateur ou au CDN.
- **Serveur** : `HybridCache` ne fait pas de SWR automatique — un client maison le fait (concept 8).

## 8. SWR Basics

Le fil rouge ajoute `SwrCache` (SharedKernel) : retourner la valeur en cache, puis rafraîchir et notifier.

```csharp
public async Task<T> GetAsync<T>(
       string key, Func<CancellationToken, Task<T>> fetch,
       TimeSpan fresh, TimeSpan maxStale, Action<T>? onRefreshed = null)
{
       if (_store.TryGetValue(key, out var entry))
       {
              var age = DateTime.UtcNow - entry.FetchedAt;
              if (age < fresh) return (T)entry.Value;                     // frais : servi
              if (age < maxStale)                                         // périmé mais utilisable
              {
                     _ = _refreshing.GetOrAdd(key, _ => RefreshAsync(key, fetch, onRefreshed));
                     return (T)entry.Value;                               // servi immédiatement
              }
       }
       var value = await fetch(CancellationToken.None);                  // absent ou trop vieux
       _store[key] = new Entry(value!, DateTime.UtcNow);
       return value;
}
```

Deux détails : le rafraîchissement dédupliqué via `_refreshing` (un seul par clé) ; `onRefreshed` appelé avec `InvokeAsync(StateHasChanged)` pour mettre à jour l'UI quand la nouvelle valeur arrive. Enregistré `AddScoped<SwrCache>()` — par circuit, comme tout état utilisateur.

## 9. CDN Caching

Un CDN stocke des copies près des utilisateurs ; on le pilote avec les en-têtes :

| En-tête | Public visé |
|---|---|
| `Cache-Control: max-age=...` | Navigateur et CDN |
| `Cache-Control: s-maxage=...` | CDN seulement (prioritaire pour lui) |
| `Surrogate-Control`, `CDN-Cache-Control` | Selon le fournisseur |
| `Vary: Accept-Encoding, Accept-Language` | Une copie par variante |

```csharp
// Page catalogue publique : le CDN garde 5 minutes, le navigateur 1 minute
ctx.Response.Headers.CacheControl = "public, max-age=60, s-maxage=300";
ctx.Response.Headers.Vary = "Accept-Encoding";
```

Trois pièges : (1) jamais `public` sur une réponse personnalisée (cookie, utilisateur connecté) ; (2) `Vary: Cookie` ruine le taux de réussite du CDN ; (3) les pages Blazor interactives contiennent des jetons antiforgery propres à chaque requête — ne les laissez pas partager par un CDN. (L'API publique arrive au module 6.)

## 10. Cache Busting

Quand un fichier change, l'ancien cache ne doit plus servir : on change l'URL en y mettant l'empreinte du contenu (`app.4f9a2c1b.css`). .NET 10 le fait automatiquement avec `MapStaticAssets` et la ressource `@Assets` :

```razor
<link rel="stylesheet" href="@Assets["app.css"]" />
```

**Vérifié dans le fil rouge** : la page sert `app.x7j1ckl8rk.css` — l'empreinte est réelle.

> **Observation développement vs production** : en environnement Development, `MapStaticAssets` sert les ressources avec `Cache-Control: no-cache` (pour voir ses modifications sans vider le cache) ; c'est en Production qu'elle envoie `max-age=31536000, immutable` + la compression gzip/brotli précalculée. Exercice 1 du cours : republier en Release et observer la différence.

> **Règle d'or** : le service worker lui-même reste à une **URL fixe** et ne doit jamais être mis en cache longtemps — le navigateur le revérifie, et c'est ainsi qu'il détecte une nouvelle version. **Vérifié : `service-worker.js` est servi `no-cache`.**

## 11. Offline First

Concevoir pour fonctionner sans réseau d'abord, puis se synchroniser. Être honnête sur Blazor Server :

| Capacité | Blazor Server | Blazor WebAssembly / PWA |
|---|---|---|
| Interface interactive sans réseau | **Non** (le circuit est le réseau) | Oui |
| Pages statiques déjà vues | Oui (service worker) | Oui |
| Page « vous êtes hors ligne » | Oui | Oui |
| Actions en file d'attente, rejouées au retour | Oui (JS + IndexedDB) | Oui |
| Données en lecture seule consultables | Oui (cache + JS) | Oui |

ShopPulse vise un **hors ligne « dégradé »** : consultation des produits déjà vus, mise en file des ajouts au panier et des favoris, resynchronisation à la reconnexion. Si l'interface hors ligne est un impératif métier, la réponse est architecturale : un client WebAssembly ou `InteractiveAuto`. Détection réseau : `wwwroot/js/offline.js` (`online`/`offline` events → callback .NET).

## 12. Background Sync

L'API Background Sync permet au service worker de rejouer une action dès que le réseau revient, même si l'onglet est fermé (`reg.sync.register("sync-queue")` + événement `sync` dans le SW). Disponibilité : partielle (surtout Chromium) — **prévoyez toujours un repli** : au retour de l'événement `online`, la page vide elle-même la file.

## 13. Precaching

Mettre en cache à l'installation du service worker les ressources dont on sait qu'elles seront nécessaires :

```javascript
const PRECACHE = ["/offline.html", "/app.css"];
self.addEventListener("install", (event) => {
   event.waitUntil(
       caches.open(STATIC_CACHE).then((cache) => cache.addAll(PRECACHE)).then(() => self.skipWaiting())
   );
});
```

Les fichiers de `MapStaticAssets` ont des noms empreints : générez la liste à la compilation (manifeste des assets) plutôt que de la maintenir à la main, sinon le précache se désynchronise. **Précachez peu** : chaque octet ajouté ralentit la première installation.

## 14. App Shell

L'app shell est l'ossature minimale (en-tête, navigation, pied) servie instantanément depuis le cache. En Blazor Server :

- Le layout est rendu côté serveur et diffusé en premier avec le streaming (module 1).
- Le service worker garde en cache une page hors ligne de repli : navigation → réseau d'abord, `catch` → `/offline.html` (implémenté dans le fil rouge).

```html
<!-- wwwroot/offline.html -->
<h1>Vous êtes hors ligne</h1>
<p>Vos actions seront envoyées dès le retour du réseau.</p>
<button onclick="location.reload()">Réessayer</button>
```

Manifeste PWA minimal dans `manifest.webmanifest` (lié depuis `App.razor`), pour l'installation sur l'écran d'accueil.

> **Piège vécu lors du module 6** : la première version du service worker appliquait le stale-while-revalidate aux navigations de pages — le navigateur servait donc du HTML **périmé** dont les marqueurs de préreendu Blazor (`prerenderId`) étaient déjà consommés côté serveur. Résultat : les îlots interactifs ne s'attachaient plus et **tous les boutons mouraient**. Règle absolue en Blazor Server : **le HTML des pages ne se met jamais en cache** (réseau pur, repli `/offline.html` si hors ligne) ; le cache du service worker ne sert que les assets empreintés. Corrigé dans `service-worker.js` (navigations en network-only, caches renommés en `shoppulse-static-v3`).

## 15. Cache Keys

Une clé de cache doit identifier **exactement** ce qui détermine la réponse. Le fil rouge : `ShopPulse.Infrastructure/CachePolicies.cs`.

```csharp
public static class CacheKeys
{
       public static string Products(string? category, int page, string culture)
              => $"products:v2:{category ?? "all"}:p{page}:{culture}";

       public static string Product(int id, string culture) => $"product:v2:{id}:{culture}";
}
```

Règles : inclure les variantes (langue, devise, rôle, catégorie, page) ; inclure une **version de schéma** (v2) pour tout invalider en cas de changement de format ; jamais d'identité utilisateur dans un cache partagé sans que ce soit voulu ; normaliser les entrées (minuscules, ordre des paramètres). Avec OutputCache, la clé se pilote avec `VaryByQuery`, `VaryByHeader`, `VaryByValue` — la politique « catalog » du fil rouge :

```csharp
o.AddPolicy("catalog", b => b
       .Expire(TimeSpan.FromMinutes(2))
       .SetVaryByQuery("category", "page", "q")
       .SetVaryByHeader("Accept-Language")
       .Tag("catalog"));
```

## 16. Versioned Assets

Deux sortes de ressources : **empreintées** (immuable → 1 an, immutable) et **stables à URL fixe** (service worker, manifest, index → court ou revalidation). Versionnez aussi les **caches du service worker** et supprimez les anciens à l'activation — implémenté dans le fil rouge (`CURRENT` + purge dans `activate`). Exercice 6 : passer `products:v2` en `v3` et constater l'invalidation complète.

## 17. Storage Quotas

Le navigateur limite l'espace. Interrogez-le (`navigator.storage.estimate()`) et demandez la persistance (`navigator.storage.persist()`, protège contre l'éviction). Principes : les écritures peuvent échouer (`QuotaExceededError`) — attrapez et prévoyez une purge ; le stockage « best effort » peut être effacé sous pression mémoire, ne le considérez pas comme une base fiable ; définissez une politique d'éviction (LRU ou plafond d'entrées).

## 18. Data Sync

Synchroniser données locales et distantes. Modèles : **Pull** (redemande périodiquement — listes simples), **Push** (le serveur notifie, SignalR, module 6 — temps réel), **Delta** (le client envoie « depuis la version X », gros volumes), **Queue/outbox** (écritures locales rejouées — hors ligne).

```csharp
// API : renvoie seulement ce qui a changé depuis un curseur de version
app.MapGet("/api/products/changes", (long since, IProductChangeLog log) =>
       Results.Ok(new { next = log.Latest, changes = log.Since(since) }));
```

Le client garde le dernier curseur et ne redemande que le delta. Chaque écriture porte un **identifiant d'idempotence** pour qu'un rejeu ne l'applique pas deux fois (module 6).

## 19. Conflict Resolution

Deux appareils modifient le même objet hors ligne : qui gagne ?

| Stratégie | Principe | Risque |
|---|---|---|
| Dernier écrit gagne | Horodatage le plus récent | Perte silencieuse |
| **Verrouillage optimiste** | Numéro de version : si périmé, rejet | Plus de code |
| Fusion par champ | Fusion champ par champ | Règles par type |
| CRDT | Structures qui fusionnent seules | Complexité |
| Demander à l'utilisateur | Afficher la différence | Friction |

Le verrouillage optimiste est le choix par défaut sensé : le serveur répond **409 Conflict** avec la version courante quand le `Version` de l'appelant ne correspond plus. Avec EF Core, une colonne `[Timestamp]`/`rowversion` lève `DbUpdateConcurrencyException`. (Exercice 5 : provoquer un 409 depuis deux navigateurs.)

## 20. Push Notifications

Le serveur envoie une notification même onglet fermé, via l'API Push et le service worker : abonnement navigateur (clés VAPID), stockage des abonnements côté serveur, `showNotification` dans le SW. Règles : permission demandée **après une action de l'utilisateur**, jamais au chargement ; clé privée VAPID côté serveur uniquement ; supprimer les abonnements qui répondent 410 Gone. (Documenté, non branché dans le fil rouge.)

---

## Récapitulatif : ShopPulse après le module 5

| Couche | Mécanisme | Où |
|---|---|---|
| Navigateur | Cache-Control, ressources empreintes | `MapStaticAssets`, `@Assets` |
| Service worker | Précache, page hors ligne, SWR, jamais `/_blazor` | `service-worker.js` |
| Appareil | IndexedDB (file d'attente), ProtectedLocalStorage | `db.js`, `BrowserDb` |
| CDN | s-maxage, Vary maîtrisé | En-têtes d'API publique (module 6) |
| Serveur | OutputCache à tags, HybridCache, SwrCache | Politique « catalog », décorateur |
| Cohérence | Clés versionnées, verrouillage optimiste | `CachePolicies`, API panier (module 6) |

**Les trois choses à retenir :**
1. **Chaque étage de cache est une promesse d'invalidation** : clés versionnées (v2), tags OutputCache, purge des anciens caches du service worker à l'activation, empreintes de contenu pour les assets.
2. **Le hors ligne Blazor Server est honnêtement dégradé** : pages vues, page de repli, file d'attente d'écritures — mais pas d'interactivité sans circuit. L'interface hors ligne complète est un choix d'architecture (WASM/Auto), pas un service worker.
3. **Le service worker a des interdits absolus en Blazor Server** : jamais `POST`, jamais `/_blazor`, jamais de cache long sur sa propre URL fixe.

## Exercices du module

1. Republier en **Release** et vérifier que `app.<hash>.css` porte `max-age=31536000, immutable` (en dev : `no-cache`, cf. concept 10).
2. Mode **Hors connexion** du navigateur : naviguer → `/offline.html` doit s'afficher.
3. Vérifier dans l'onglet Application que `/_blazor` n'apparaît jamais dans le cache du service worker.
4. Ajout au panier hors ligne : mise en file IndexedDB (`db.js` + `BrowserDb`), rejouée au retour du réseau (`js/offline.js` → événement `online`).
5. Conflit : même panier modifié depuis deux navigateurs → vérifier la réponse 409 (API du module 6).
6. Changer `products:v2` en `v3` dans `CachePolicies` : invalidation complète du cache.
