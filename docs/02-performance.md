# Chapitre 2 — Module 2 : Performance

> **Question du module :** comment rendre ShopPulse rapide à charger, fluide à l'utilisation, et comment le prouver avec des mesures ?
> **Fil rouge :** images priorisées, squelettes anti-CLS, recherche avec debounce, liste virtualisée, cache serveur, instrumentation.

## Le changement de profil : Blazor Server vs SPA JavaScript

Avant de choisir une optimisation, il faut savoir *où* coûte une application. Le profil de Blazor Server est l'inverse d'une SPA :

| Aspect | SPA JS / WASM | Blazor Server |
|---|---|---|
| Poids à télécharger | Gros (bundle JS ou runtime .NET) | Petit (`blazor.web.js` + CSS + images) |
| Coût d'une interaction | Local au navigateur | Un aller-retour réseau (SignalR) |
| Goulot principal | Taille du bundle, CPU client | Latence réseau, CPU et mémoire **serveur** |

**Conséquence :** les optimisations classiques « taille du bundle » cèdent la place aux images, au TTFB, à la latence des interactions et à la charge serveur. Toutes les décisions du module découlent de ce tableau.

## Préparation du fil rouge : pagination et recherche

Le catalogue gagne deux capacités côté service — `PagedResult<T>` et `GetPageAsync` (skip/take/search) — qui serviront à la fois à la virtualisation (concept 15) et au debounce (concept 13). La latence de `GetPageAsync` est de 150 ms (réaliste), celle de `GetAllAsync` reste à 1,5 s (outil pédagogique du chapitre 1, recyclé ici pour démontrer le cache).

## 1. Core Web Vitals

Trois métriques de Google, mesurées sur l'expérience **réelle** au 75e percentile des utilisateurs :

| Métrique | Mesure | « Bon » |
|---|---|---|
| **LCP** | Vitesse d'affichage du plus gros élément | 2,5 s |
| **INP** | Réactivité aux interactions | 200 ms |
| **CLS** | Stabilité visuelle | 0,1 |

Le TTFB n'en fait pas partie, mais il conditionne le LCP (bon : 0,8 s). Les concepts 2 à 5 les détaillent, les concepts 17 à 20 expliquent comment les mesurer.

## 2. LCP (Largest Contentful Paint)

Sur ShopPulse, l'élément LCP est généralement la première image produit. Trois leviers : la livrer tôt, la prioriser, éviter qu'elle dépende du circuit. Dans le fil rouge, `ProductCard` accepte un paramètre `Priority` :

```razor
<img src="@Product.ImageUrl"
         alt="@Product.Name"
         width="200" height="200"
         loading="@(Priority ? "eager" : "lazy")"
         fetchpriority="@(Priority ? "high" : "auto")"
         decoding="async" />
```

```razor
@* Catalog.razor : seules les 4 premières cartes (visibles sans scroll) sont prioritaires *@
@foreach (var (p, i) in _products.Select((p, i) => (p, i)))
{
       <ProductCard @key="p.Id" Product="p" Priority="@(i < 4)" />
}
```

Règles : ne jamais mettre `loading="lazy"` sur l'image LCP ; rendre la page en Static SSR (l'image est dans le HTML initial) ; précharger l'image héro avec `<link rel="preload" as="image">` (concept 11). **Vérifié dans le fil rouge : la page sert 4 images `eager` et 196 `lazy`.**

## 3. INP (Interaction to Next Paint)

La métrique la plus sensible en Blazor Server, car chaque clic traverse le réseau. Stratégies :

**a) Feedback immédiat avant le travail long** — rendre l'état « occupé » tout de suite, puis laisser le rendu partir avant le travail :

```csharp
private async Task AddAsync()
{
       _busy = true;
       StateHasChanged();          // pousse le rendu "occupé" tout de suite
       await Task.Yield();         // laisse le rendu partir avant le travail
       await Cart.AddAsync(ProductId);
       _busy = false;
}
```

**b) Ne jamais passer par le serveur pour de l'UI pure.** Un menu déroulant n'a pas besoin de C# : `<details><summary>Filtres</summary>…</details>` fonctionne sans circuit.

**c) Éviter les rendus coûteux dans un gestionnaire** : `ShouldRender`, `@key`, `Virtualize` (concepts 13 et 16 du module 1).

## 4. CLS (Cumulative Layout Shift)

Causes typiques dans ShopPulse : images sans dimensions ; le streaming SSR (le « Chargement... » remplacé par une grille plus grande) ; les polices qui changent la taille du texte ; l'UI de reconnexion Blazor insérée dans le flux. Correctifs du fil rouge :

```css
/* Réserver l'espace du contenu streamé */
.grid { min-height: 900px; display: grid; gap: 1rem;
              grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); }

.card img { aspect-ratio: 1 / 1; width: 100%; height: auto; }   /* géométrie connue avant le chargement */

/* Squelette de même géométrie que les vraies cartes : pas de saut à la substitution */
.skeleton { height: 320px; border-radius: 8px;
                      background: linear-gradient(90deg,#eee 25%,#f6f6f6 50%,#eee 75%);
                      background-size: 200% 100%; animation: shimmer 1.2s infinite; }

/* L'UI de reconnexion ne doit pas pousser le contenu */
#components-reconnect-modal { position: fixed; inset: 0; }
```

```razor
@if (_products is null)
{
       <div class="grid" aria-busy="true">
              @for (var i = 0; i < 8; i++) { <div class="skeleton"></div> }
       </div>
}
```

> **Limite** : l'animation du squelette sera neutralisée au module 8 (`prefers-reduced-motion`).

## 5. TTFB (Time To First Byte)

Le TTFB dépend du serveur : base de données, rendu, middlewares. Moyens d'agir : output cache des pages statiques (module 1), code **entièrement asynchrone** (jamais `.Result` ni `.Wait()`), `[StreamRendering]` pour envoyer le squelette avant les données. Pour *voir* où va le temps, un middleware émet un en-tête `Server-Timing` :

```csharp
app.Use(async (ctx, next) =>
{
       var sw = System.Diagnostics.Stopwatch.StartNew();
       ctx.Response.OnStarting(() =>
       {
              ctx.Response.Headers["Server-Timing"] = $"app;dur={sw.ElapsedMilliseconds}";
              return Task.CompletedTask;
       });
       await next();
});
```

**Vérifié dans le fil rouge :** `curl` montre `Server-Timing: app;dur=4` — le temps serveur est lisible directement dans l'onglet Network des DevTools.

## 6. Code Splitting

Une SPA découpe son bundle JS ; Blazor Server n'envoie pas de C# au navigateur, donc le lazy loading d'assemblies ne concerne que WebAssembly/Auto. Ce qui reste pertinent :

**a) Découpage par render mode** — une page Static SSR n'ouvre aucun circuit : chaque page qui reste statique est « splittée » naturellement. C'est l'argument structurel de l'architecture en îlots (module 1).

**b) JavaScript à la demande via des modules ES** — un composant qui importe son module ne le télécharge que lorsqu'il apparaît :

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
       if (!firstRender) return;
       _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/stockChart.js");
       await _module.InvokeVoidAsync("draw", _id, new[] { 10, 24, 18, 31 });
}

public async ValueTask DisposeAsync()
{
       if (_module is not null)
              try { await _module.DisposeAsync(); } catch (JSDisconnectedException) { }
}
```

> **Piège** : le circuit peut être fermé avant le `Dispose` — d'où le `try/catch` sur `JSDisconnectedException`. C'est une fuite de ressources classique en Blazor Server.

## 7. Tree Shaking

Supprimer le code jamais appelé, couche par couche :

| Couche | Outil |
|---|---|
| JS personnalisé | esbuild / Rollup |
| CSS | PurgeCSS, ou Tailwind qui scanne les `.razor` |
| C# côté serveur | Rien à faire (non envoyé au client) |
| C# WASM | IL Trimming (`PublishTrimmed`) |

```bash
npx esbuild src/js/stockChart.js --bundle --minify --format=esm --outfile=wwwroot/js/stockChart.js
```

Notez le point structurant : **en Blazor Server, le tree shaking du C# est sans objet** — un argument de plus dans le tableau du début du chapitre.

## 8. Lazy Loading

Trois niveaux : **a)** images natives (`loading="lazy"` + dimensions) ; **b)** composants chargés à la demande (`@if (_showReviews) { <Reviews … /> }` ne coûte rien tant qu'il n'est pas affiché) ; **c)** données chargées à la demande avec `Virtualize` (concept 15).

## 9. Image Optimization

Les images pèsent généralement plus que tout le reste. Checklist : formats modernes (AVIF, WebP, JPEG en repli), tailles adaptées (`srcset`/`sizes`), dimensions explicites (CLS), CDN avec cache long.

```razor
<picture>
       <source type="image/webp" srcset="@ImageUrl(400, "webp") 400w, @ImageUrl(800, "webp") 800w"
                      sizes="(max-width:600px) 100vw, 200px" />
       <img src="@ImageUrl(400, "jpg")" alt="@Product.Name" width="200" height="200"
                loading="lazy" decoding="async" />
</picture>
```

Le cours montre un endpoint de redimensionnement avec SixLabors.ImageSharp (`w` borné par `Math.Clamp` — *jamais faire confiance au client*), servi avec `CacheOutput` et `SetVaryByQuery`.

> **Limite du fil rouge** : ShopPulse s'arrête à `loading`/`fetchpriority`/`aspect-ratio` faute d'images réelles. L'endpoint de redimensionnement et le `<picture>` sont documentés mais non branchés ; en production, un CDN d'images est de toute façon préférable à un endpoint maison (support AVIF incomplet dans ImageSharp).

## 10. Font Loading

Une police web bloque ou décale le texte. Règles : `font-display: swap`, format woff2 uniquement, auto-hébergement, `preload` de la police principale.

```html
<link rel="preload" href="/fonts/inter-var.woff2" as="font" type="font/woff2" crossorigin>
```

> **Piège** : l'attribut `crossorigin` est **obligatoire** pour les polices, même en même origine. Sans lui, la police est téléchargée deux fois (une fois par le preload, une fois par la page).

## 11. Prefetch and Preload

- **preload** : « j'aurai besoin de ça tout de suite » (ressource de la page courante).
- **prefetch** : « j'en aurai probablement besoin bientôt » (page suivante, basse priorité).
- **preconnect** : ouvrir à l'avance la connexion vers un autre domaine.

`<ResourcePreloader />` (présent dans `App.razor` depuis le module 1) génère les préchargements des assets fingerprintés. Le cours montre un prefetch au survol du lien « Voir » :

```javascript
window.prefetchPage = (url) => {
   const l = document.createElement("link");
   l.rel = "prefetch"; l.href = url;
   document.head.appendChild(l);
};
```

> **Limite** : la version `@onmouseenter` fait un aller-retour serveur juste pour du JS — acceptable pour comprendre la mécanique, mais un écouteur JS direct serait plus économique. La règle générale : ne jamais payer un circuit pour de l'UI pure (cf. concept 3).

## 12. Compression

Les assets statiques servis via `MapStaticAssets()` (.NET 9+) sont **déjà précompressés** en gzip et Brotli au build. Pour le HTML dynamique, `AddResponseCompression` existe, mais :

> **Piège** : la compression sur HTTPS est désactivée par défaut à cause des attaques BREACH/CRIME, qui peuvent exposer des secrets réfléchis dans la page (jeton antiforgery, par exemple). La pratique recommandée : compresser au niveau du reverse proxy ou du CDN (Nginx, Front Door, Cloudflare), pas dans l'app. Les messages SignalR (WebSocket) ne sont pas concernés par ce middleware.

## 13. Debounce and Throttle

- **Debounce** : attendre que l'utilisateur s'arrête (recherche).
- **Throttle** : au plus une exécution par intervalle (scroll, resize).

Le fil rouge ajoute un utilitaire réutilisable, `Infrastructure/Debouncer.cs`, fondé sur un `CancellationTokenSource` remplacé à chaque frappe :

```csharp
public sealed class Debouncer : IDisposable
{
       private CancellationTokenSource? _cts;

       public async Task RunAsync(Func<CancellationToken, Task> action, TimeSpan delay)
       {
              _cts?.Cancel();
              _cts?.Dispose();
              _cts = new CancellationTokenSource();
              var token = _cts.Token;
              try
              {
                     await Task.Delay(delay, token);
                     await action(token);
              }
              catch (OperationCanceledException) { /* remplacé par une saisie plus récente */ }
       }

       public void Dispose() { _cts?.Cancel(); _cts?.Dispose(); }
}
```

Utilisé dans la recherche du catalogue virtualisé avec `@bind:event="oninput" @bind:after="OnSearchChanged"` et un délai de 300 ms.

> **Limite** : avec `oninput`, *chaque frappe* est quand même envoyée au serveur — le debounce côté C# évite le travail inutile, pas le trafic SignalR. Pour réduire aussi le trafic : `@bind:event="onchange"` ou un debounce côté JS. (Exercice du module : compter les messages WebSocket avant/après.)

## 14. Memoization

Mémoriser, c'est ne pas recalculer ce qui n'a pas changé. Trois niveaux :

**a) Dans le composant** — recalculer seulement quand les entrées changent (mémoiser la catégorie et une version, pas le calcul). **b) Empêcher le re-rendu d'un enfant** — `ShouldRender` comparé par référence ; passer des objets complexes force le re-rendu. **c) Côté serveur** — `IMemoryCache` ou **`HybridCache`**, qui ajoute la protection contre les *cache stampedes* (les calculs concurrents identiques ne s'exécutent qu'une fois).

Le fil rouge décore `IProductService` sans toucher aux consommateurs — c'est tout l'intérêt de l'injection de dépendances posée au chapitre 0 :

```csharp
public class CachedProductService(IProductService inner, HybridCache cache) : IProductService
{
       public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default) =>
              await cache.GetOrCreateAsync("products:all",
                     async token => await inner.GetAllAsync(token),
                     new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
                     tags: ["catalog"], cancellationToken: ct);
       // les autres méthodes délèguent à `inner`
}
```

```csharp
// Enregistrement en décorateur : l'implémentation concrète d'abord, l'interface ensuite
builder.Services.AddSingleton<InMemoryProductService>();
builder.Services.AddSingleton<IProductService>(sp =>
       new CachedProductService(sp.GetRequiredService<InMemoryProductService>(),
              sp.GetRequiredService<HybridCache>()));
builder.Services.AddHybridCache();
```

**Vérifié dans le fil rouge :** `/catalog` passe de ~1,7 s (cache froid) à ~0,1 s (cache chaud). Le package `Microsoft.Extensions.Caching.Hybrid` est nécessaire (hors shared framework).

> **Piège** : `HybridCache` n'est pas dans le framework partagé de .NET 10 — il faut ajouter le paquet NuGet, et penser au `using Microsoft.Extensions.Caching.Hybrid;` côté `Program.cs`.

## 15. Virtualized Lists

`<Virtualize>` ne garde dans le DOM que les lignes visibles. Pour de gros volumes, `ItemsProvider` charge par tranches — c'est la page `/catalog-virtual` du fil rouge, branchée sur `GetPageAsync` :

```razor
<div class="viewport">
       <Virtualize @ref="_virtualize" ItemsProvider="LoadProducts" ItemSize="220"
                           OverscanCount="4" Context="p">
              <ItemContent><ProductCard @key="p.Id" Product="p" /></ItemContent>
              <Placeholder><div class="skeleton" style="height:220px"></div></Placeholder>
       </Virtualize>
</div>
```

```csharp
private async ValueTask<ItemsProviderResult<Product>> LoadProducts(ItemsProviderRequest req)
{
       var page = await Products.GetPageAsync(req.StartIndex, req.Count, _search, req.CancellationToken);
       return new ItemsProviderResult<Product>(page.Items, page.Total);
}
```

Règles : conteneur à hauteur fixe et `overflow: auto` (`.viewport { height: 80vh; }`) ; `ItemSize` proche de la hauteur réelle sinon le défilement saute ; `Virtualize` est un composant **interactif** — sans circuit, il n'a pas de sens.

## 16. Web Workers

Un Web Worker exécute du JavaScript hors du thread principal (traitement d'image, gros tri). En Blazor Server, le C# ne tourne pas dans le navigateur : un worker n'est pertinent que pour du JS. Le calcul C# lourd se règle côté serveur (`Task.Run`, service en arrière-plan) sans bloquer d'autres circuits. Le cours montre la mécanique complète (module ES, `DotNetObjectReference`, `[JSInvokable]`, nettoyage dans `DisposeAsync`) avec un calcul de moyenne/médiane — volontairement overkill pour l'exemple.

## 17. Performance Budgets

Un budget est une **limite chiffrée qui fait échouer le build** en cas de dépassement. Budgets de ShopPulse :

| Élément | Budget |
|---|---|
| JS (hors blazor.web.js) | 50 Ko |
| CSS | 30 Ko |
| Images au-dessus de la ligne de flottaison | 300 Ko |
| LCP (mobile 4G) | 2,5 s |
| Message SignalR par interaction | 5 Ko (à surveiller dans l'onglet WS) |

Un `budget.json` Lighthouse encode ces limites (`resourceSizes` en Ko, `timings` avec `largest-contentful-paint` et `cumulative-layout-shift`). Le budget « script » inclut `blazor.web.js`, d'où une valeur plus large. (Exercice : écrire le budget et faire échouer volontairement l'audit.)

## 18. Bundle Analysis

Ce qu'il faut inspecter pour une app Blazor Server : **onglet Network** (trier par taille, repérer les assets inutiles) ; **onglet Coverage** (part de CSS/JS réellement utilisée) ; **frames WebSocket** (un lot de rendu de plusieurs dizaines de Ko par interaction signale un composant qui re-rend trop) ; `--metafile` d'esbuild pour un bundle JS, analysable sur esbuild.github.io/analyze/.

## 19. Lighthouse

Lighthouse audite performance, accessibilité, bonnes pratiques et SEO — en mode **production**, jamais avec `dotnet watch` :

```bash
dotnet publish -c Release -o out
ASPNETCORE_ENVIRONMENT=Production dotnet out/ShopPulse.dll --urls https://localhost:5001
npx lighthouse https://localhost:5001/catalog \
   --budget-path=budget.json --preset=desktop --output=html --output-path=report.html
```

Tester aussi en mobile (réseau et CPU bridés). Les résultats varient d'un run à l'autre : comparer les médianes de plusieurs exécutions.

> **Limite** : Lighthouse est une mesure de laboratoire. Elle ne voit ni les vrais réseaux, ni les vrais appareils, ni les temps de réponse sous charge — d'où le concept suivant.

## 20. Real User Monitoring (RUM)

Le RUM mesure les vrais utilisateurs : un petit script (web-vitals, auto-hébergé) collecte les métriques et les envoie par `navigator.sendBeacon` à un endpoint minimal :

```csharp
public record RumMetric(string Name, double Value, string Rating, string Page, string Id);

app.MapPost("/rum", (RumMetric m, ILogger<Program> log) =>
{
       log.LogInformation("RUM {Name}={Value:F1} ({Rating}) on {Page}", m.Name, m.Value, m.Rating, m.Page);
       return Results.NoContent();
}).DisableAntiforgery();
```

`DisableAntiforgery()` est acceptable ici (l'endpoint ne modifie rien), mais il faut valider les entrées et limiter le débit (modules 6 et 7), sinon n'importe qui peut polluer les statistiques. Côté serveur, OpenTelemetry expose les métriques de circuits Blazor (`Microsoft.AspNetCore.Components.Server.Circuits` : circuits actifs, durée) vers Prometheus ou Application Insights.

---

## Récapitulatif : ce que ShopPulse a gagné

| Avant | Après |
|---|---|
| Images sans dimensions, toutes chargées | `loading`, `fetchpriority`, ratio fixe |
| Grille non virtualisée | `Virtualize` + `ItemsProvider` |
| Une requête par frappe | Debounce 300 ms |
| Pas de mesure | `Server-Timing`, Lighthouse + budget, RUM |
| Données rechargées à chaque visite | `HybridCache` + output cache |

**Les trois choses à retenir :**
1. **Le profil de performance de Blazor Server est à l'envers d'une SPA** : on optimise les images, le TTFB, la latence d'interaction et le serveur — pas la taille du bundle.
2. **La latence perçue se joue avant les données** : streaming + squelettes à géométrie fixe (anti-CLS) + feedback immédiat (INP).
3. **On ne peut améliorer que ce qu'on mesure** : `Server-Timing` côté serveur, budgets Lighthouse côté build, RUM côté utilisateurs réels.

## Exercices du module (à faire avec les DevTools et Lighthouse)

1. Mesurer LCP, CLS et TTFB avec Lighthouse avant/après les dimensions d'images et le squelette.
2. Onglet WebSocket : taper dans la recherche sans debounce, puis avec — compter les messages.
3. ✅ fait en pair programming : vérifier que le 2e appel ne coûte plus 1,5 s (1,7 s → 0,1 s mesurés).
4. Écrire un `budget.json` et faire échouer volontairement un audit en ajoutant une grosse image.
5. Ajouter `rum.js` et afficher les métriques dans les logs pendant la navigation.
