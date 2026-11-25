# Chapitre 1 — Module 1 : Rendering (le rendu)

> **Question du module :** où et quand le HTML est-il produit, et comment reste-t-il synchronisé avec l'état ?
> **Fil rouge :** catalogue en streaming, fiche produit avec îlot interactif, composant générique de chargement.
> **Fichiers du projet :** `Components/Pages/Catalog.razor`, `Components/Pages/ProductDetail.razor`, `Components/Shared/ProductCard.razor`, `Components/Shared/AddToCartButton.razor`, `Components/Shared/AsyncBoundary.razor`.

---

## 1. CSR vs SSR

**CSR (Client-Side Rendering)** : le navigateur reçoit un HTML presque vide, télécharge le JavaScript de l'application, puis construit la page. C'est le modèle de Blazor WebAssembly — et de React, Angular, Vue.

**SSR (Server-Side Rendering)** : le serveur exécute le code et envoie du HTML déjà construit, lisible immédiatement.

Blazor Web App ne choisit pas un camp : il propose **quatre modes**, sélectionnables par page ou par composant :

| Mode | Où s'exécute le C# | Interactif ? |
|---|---|---|
| Static SSR | Serveur, une requête | Non (liens et formulaires seulement) |
| InteractiveServer | Serveur, circuit SignalR | Oui |
| InteractiveWebAssembly | Navigateur | Oui |
| InteractiveAuto | Serveur puis WASM | Oui |

Ce cours vise Blazor Server, donc la combinaison **Static SSR + InteractiveServer** :

```razor
@page "/catalog"
@rendermode InteractiveServer   @* circuit SignalR ouvert, événements gérés en C# *@
```

Une page sans `@rendermode` est en Static SSR : HTML pur, zéro circuit, zéro coût d'état. C'est la règle de base : **commencer statique, rendre interactif sur besoin** (règle d'or n° 1 du cours).

## 2. Static Generation (SSG)

Le HTML est produit **au build**, pas à la requête. Blazor n'a pas de SSG natif ; deux équivalents :

1. **Output caching** d'une page Static SSR : le HTML est généré une fois puis servi pour les requêtes suivantes (voir concept 3).
2. Un générateur externe qui « crawle » l'application et écrit des fichiers `.html` (Statiq, ou un script `HttpClient` en CI).

```razor
@page "/about"
@attribute [OutputCache(Duration = 3600)]
```

## 3. Incremental Static Regeneration (ISR)

L'ISR régénère une page statique **en arrière-plan, sans redéployer**. L'équivalent .NET : l'output cache avec **tags** et invalidation à la demande :

```csharp
builder.Services.AddOutputCache(o =>
       o.AddPolicy("Catalog", b => b
              .Expire(TimeSpan.FromMinutes(5))
              .Tag("catalog")));
```

```razor
@page "/catalog-static"
@attribute [OutputCache(PolicyName = "Catalog")]
```

```csharp
// Quand un produit change, on invalide
app.MapPost("/admin/revalidate", async (IOutputCacheStore store, CancellationToken ct) =>
{
       await store.EvictByTagAsync("catalog", ct);
       return Results.Ok();
});
```

**Limite** : contrairement à l'ISR de Next.js, la page n'est pas servie « stale » pendant la régénération — la première requête après éviction paie le coût du rendu. (Approfondi au module 5.)

## 4. Streaming SSR

Le serveur envoie d'abord le **squelette** de la page, puis les parties lentes dès qu'elles sont prêtes, **sur la même réponse HTTP**. En Blazor, `[StreamRendering]` fait exactement cela — et c'est la raison d'être de la latence simulée de 1,5 s dans `InMemoryProductService` (chapitre 0) : sans elle, le streaming serait invisible.

`Components/Pages/Catalog.razor` :

```razor
@page "/catalog"
@attribute [StreamRendering]
@inject IProductService Products

<PageTitle>Catalogue — ShopPulse</PageTitle>
<h1>Catalogue</h1>

@if (_products is null)
{
       <p><em>Chargement des produits...</em></p>
}
else
{
       <div class="grid">
              @foreach (var p in _products)
              {
                     <ProductCard @key="p.Id" Product="p" />
              }
       </div>
}

@code {
       private IReadOnlyList<Product>? _products;

       protected override async Task OnInitializedAsync()
              => _products = await Products.GetAllAsync();
}
```

Sans `[StreamRendering]`, la page attend 1,5 s avant d'afficher quoi que ce soit. Avec lui, le titre et « Chargement... » s'affichent immédiatement, puis la grille remplace le message quand `GetAllAsync` rend la main.

> **Piège** : le remplacement dans le navigateur exige que `blazor.web.js` soit chargé — c'est le cas dans `App.razor`, où le script est en bas du `<body>` (voir concept 10). Sans ce script, le client reçoit le squelette mais ne sait pas le mettre à jour.

## 5. Hydration (et le double appel de OnInitializedAsync)

Dans React, l'hydration attache les événements à du HTML prérendu. **Blazor Server ne fait pas d'hydratation** : après le prérendu, un circuit SignalR s'ouvre, le composant est **recréé côté serveur**, et le DOM existant est repris par un diff.

**Conséquence directe : `OnInitializedAsync` s'exécute deux fois** (prérendu, puis circuit). Les données sont chargées deux fois et l'écran peut clignoter. La solution est `PersistentComponentState`, qui transporte l'état du prérendu vers le circuit :

```razor
@page "/catalog-interactive"
@rendermode InteractiveServer
@implements IDisposable
@inject IProductService Products
@inject PersistentComponentState ApplicationState

@code {
       private IReadOnlyList<Product>? _products;
       private PersistingComponentStateSubscription _sub;

       protected override async Task OnInitializedAsync()
       {
              _sub = ApplicationState.RegisterOnPersisting(() =>
              {
                     ApplicationState.PersistAsJson("products", _products);
                     return Task.CompletedTask;
              });

              if (ApplicationState.TryTakeFromJson<List<Product>>("products", out var restored))
                     _products = restored;                     // 2e exécution : pas de rechargement
              else
                     _products = await Products.GetAllAsync(); // 1re exécution (prérendu)
       }

       public void Dispose() => _sub.Dispose();
}
```

Le principe : au premier passage, on persiste l'état dans le HTML ; au second, on le **récupère** au lieu de recharger. Le cours signale que .NET 10 ajoute une version déclarative par attribut qui évite ce code répétitif — vérifier le nom exact dans la documentation de votre version.

## 6. Partial Hydration

Seuls certains composants deviennent interactifs. En Blazor c'est natif : `@rendermode` peut être placé **sur l'instance du composant** plutôt que sur la page. Toute notre fiche produit est statique ; un seul bouton ouvre un circuit :

```razor
@page "/product/{Id:int}"
@inject IProductService Products

<h1>@_product?.Name</h1>          @* HTML statique *@
<p>@_product?.Description</p>

<AddToCartButton ProductId="Id" @rendermode="InteractiveServer" /> @* seul îlot interactif *@
```

> **Piège** : un composant interactif doit recevoir uniquement des **paramètres sérialisables** — ils traversent la frontière statique/interactive. Pas d'injection de `DbContext` ni de délégués non sérialisables en paramètre.

## 7. Islands Architecture

C'est le concept précédent appliqué **comme architecture** : des pages majoritairement statiques parsemées d'« îlots » interactifs. Les règles de ShopPulse :

- Pages de contenu (accueil, à propos, fiche produit) : **Static SSR**.
- Îlots : bouton panier, filtres, widget de stock live.
- Les îlots communiquent par un **service d'état** (module 3), jamais par des paramètres en cascade à travers la frontière statique/interactive.

Premier îlot du fil rouge, `Components/Shared/AddToCartButton.razor` :

```razor
<button @onclick="Add" disabled="@(_added)">
       @(_added ? "Ajouté ✔" : "Ajouter au panier")
</button>

@code {
       [Parameter] public int ProductId { get; set; }
       private bool _added;
       private void Add() => _added = true; // sera branché au CartStore au module 3
}
```

## 8. React Server Components

Les RSC sont des composants qui ne s'exécutent que sur le serveur, accèdent à la base directement et n'envoient aucun code au client. **Un composant Static SSR de Blazor est conceptuellement la même chose** : il s'exécute côté serveur, peut injecter un `DbContext`, et le client ne reçoit que du HTML.

Et dans Blazor Server, même les composants interactifs restent côté serveur : il n'y a **pas de frontière code serveur/code client à surveiller**, contrairement à Blazor WebAssembly ou au mode Auto, où un secret injecté dans un composant peut fuiter dans le navigateur. (Sécurité : module 7.)

## 9. Edge Rendering

Le rendu est exécuté au plus près de l'utilisateur (CDN, fonctions edge). Pour Blazor :

- **Static SSR : compatible CDN.** On met en cache le HTML en tête (Azure Front Door, Cloudflare) avec `Cache-Control`.
- **InteractiveServer : pas adapté à l'edge pur.** Le circuit est *stateful* : il exige des sessions collantes (affinité) et une faible latence, car chaque clic fait un aller-retour SignalR.

Pour un public mondial : servez le contenu en Static SSR cachable, et réservez les circuits aux zones vraiment interactives. C'est la même règle que l'architecture en îlots, appliquée à la topologie.

## 10. Critical Rendering Path

La séquence de rendu du navigateur : HTML → CSS (**bloquant** le rendu) → JS → DOM + CSSOM → layout → paint. Pour la raccourcir, `App.razor` du fil rouge applique trois règles :

```html
<head>
       <meta charset="utf-8" />
       <base href="/" />
       <ResourcePreloader />              <!-- preload automatique des assets (.NET 9+) -->
       <link rel="stylesheet" href="@Assets["app.css"]" />
       <ImportMap />
       <HeadOutlet />                     <!-- PageTitle / HeadContent des pages -->
</head>
<body>
       <Routes />
       <script src="_framework/blazor.web.js"></script>  <!-- en bas : non bloquant -->
</body>
```

1. **CSS critique léger dans le `<head>`** — le rendu attend le CSS, pas le JS.
2. **Aucun script bloquant au-dessus du contenu** — `blazor.web.js` est en bas du `<body>`.
3. **Static SSR sur les pages d'entrée** — le contenu est visible avant même que le circuit s'ouvre.

L'helper `@Assets[...]` génère des URL à empreinte (fingerprint) pour un cache navigateur sûr, et `<ResourcePreloader />` précharge les assets critiques.

## 11. Reflow and Repaint

- **Reflow (layout)** : le navigateur recalcule tailles et positions. Coûteux.
- **Repaint** : le navigateur redessine sans changer la géométrie. Moins coûteux.

Blazor ne modifie que les nœuds DOM qui ont changé (concept 12), ce qui limite déjà les reflows. Ce qu'il faut éviter soi-même :

```razor
@* Mauvais : width/height modifiés à chaque interaction → reflow *@
<div style="width:@(_w)px">...</div>

@* Mieux : transform → pas de reflow *@
<div style="transform:scaleX(@_ratio)">...</div>
```

Règle : animer et dimensionner avec `transform` et `opacity` (composition GPU, concept 15) ; ne toucher à la géométrie (width, height, top, left) que si nécessaire.

## 12. Virtual DOM

Blazor n'a pas de Virtual DOM au sens React, mais l'idée est la même : un **RenderTree en mémoire**. À chaque rendu, Blazor construit un nouvel arbre via `RenderTreeBuilder`, le compare à l'ancien (**diff**), puis envoie au navigateur uniquement un lot d'éditions binaires par SignalR.

Un composant Razor est compilé grosso modo comme ceci :

```csharp
protected override void BuildRenderTree(RenderTreeBuilder builder)
{
       builder.OpenElement(0, "h3");
       builder.AddContent(1, Product.Name);
       builder.CloseElement();
}
```

On n'écrit presque jamais ce code à la main — mais il explique **pourquoi les gros arbres coûtent cher**, et pourquoi `ShouldRender` (concept 16) et `@key` (concept 13) existent : ce sont les deux leviers pour réduire le coût du diff.

## 13. Reconciliation

La réconciliation est l'algorithme qui décide quels nœuds **garder, déplacer ou détruire**. Pour les listes, `@key` est indispensable : sans lui, Blazor compare par **position**, donc insérer un élément en tête re-rend toute la liste.

```razor
@foreach (var p in _filtered)
{
       <ProductCard @key="p.Id" Product="p" />
}
       @* identité stable : seul le nouvel élément est créé, les autres sont réutilisés *@
```

> **Piège** : utiliser un identifiant métier stable (ici `p.Id`), **jamais l'index de boucle**. `@key="i"` revient à la comparaison par position, donc à ne rien faire — et ça détruit l'état interne des composants (focus, saisie, animation).

## 14. Layout Thrashing

Alterner lecture (`offsetHeight`) et écriture (`style.height`) du layout dans une boucle force un reflow à chaque itération. En Blazor, le piège vient souvent de **l'interop JS** : appeler JS N fois depuis C# multiplie les allers-retours SignalR *et* les reflows.

```csharp
// Mauvais : N allers-retours + N reflows
foreach (var id in ids)
       await JS.InvokeVoidAsync("resizeCard", id);

// Bon : un seul appel, lectures groupées puis écritures groupées
await JS.InvokeVoidAsync("resizeCards", ids);
```

```javascript
// wwwroot/layout.js
export function resizeCards(ids) {
   const els = ids.map(id => document.getElementById(id));
   const heights = els.map(e => e.offsetHeight);              // lectures groupées
   els.forEach((e, i) => e.style.minHeight = heights[i] + "px"); // puis écritures
}
```

Principe général : **lire tout, puis écrire tout** — et regrouper les appels interop.

## 15. Paint and Composite

Le navigateur peint les calques puis les **compose** sur le GPU. Les propriétés `transform` et `opacity` n'agissent que sur la composition : elles sont très peu coûteuses.

```css
.card { transition: transform .2s ease, opacity .2s ease; }
.card:hover { transform: translateY(-4px); }  /* composite uniquement, pas de reflow */

.drawer { will-change: transform; }           /* à utiliser avec parcimonie */
```

`will-change` réserve un calque GPU, ce qui consomme de la mémoire : sur des listes longues, l'effet est inverse (une carte du catalogue en volonté de changement, oui ; 200, non).

## 16. Lazy Rendering

Ne rendre que ce qui est nécessaire. Trois outils en Blazor :

```razor
@* 1. Virtualize : seul le visible est dans le DOM *@
<div style="height:600px; overflow:auto">
       <Virtualize Items="_products" Context="p" ItemSize="220">
              <ProductCard Product="p" />
       </Virtualize>
</div>

@* 2. Rendu conditionnel : ne pas payer ce qui est caché *@
@if (_showReviews) { <Reviews ProductId="Id" /> }
```

```csharp
// 3. Court-circuiter le diff quand rien n'a changé
protected override bool ShouldRender() => _dirty;
```

Pour 200 produits, la différence est déjà mesurable ; pour 20 000, `<Virtualize>` est indispensable. (Exercice du module : comparer le nombre de nœuds DOM.)

## 17. Pre-rendering

Le serveur génère le HTML initial d'un composant interactif **avant l'ouverture du circuit**. C'est activé par défaut, et c'est souvent ce qu'on veut (SEO, premier affichage). Pour le désactiver sur un composant qui dépend de JS interop :

```razor
@rendermode @(new InteractiveServerRenderMode(prerender: false))
```

> **Piège classique** : le **JS interop est interdit pendant le prérendu** — il n'y a pas encore de navigateur à l'autre bout. Il faut attendre `OnAfterRenderAsync` :

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
       if (firstRender)
              _theme = await JS.InvokeAsync<string>("localStorage.getItem", "theme");
}
```

## 18. SEO and Rendering

Les robots d'indexation lisent mieux le HTML que le JavaScript. Static SSR et le prérendu donnent un **HTML complet dès la première réponse**, avantage décisif sur Blazor WebAssembly pur. Notre fiche produit en profite :

```razor
@page "/product/{Id:int}"
@inject IProductService Products

<PageTitle>@_p?.Name — ShopPulse</PageTitle>
<HeadContent>
       <meta name="description" content="@_p?.Description" />
       <link rel="canonical" href="https://shoppulse.example/product/@Id" />
       <script type="application/ld+json">
           { "@@context":"https://schema.org","@@type":"Product","name":"@_p?.Name" }
       </script>
</HeadContent>
```

`PageTitle` et `HeadContent` sont rendus via `<HeadOutlet />` dans `App.razor`. Ajouter aussi un `sitemap.xml` (endpoint minimal) et un `robots.txt`.

> **Limite (signalée par le cours)** : le JSON-LD construit par interpolation est fragile et le hreflang/canonical par langue n'existe pas encore. Le module 7 corrige la construction du JSON-LD, le module 8 la canonique multilingue.

## 19. Suspense Boundaries

React a `<Suspense>` pour afficher un *fallback* pendant qu'une partie charge. Blazor n'a pas de Suspense natif ; on l'imite avec trois briques : **streaming rendering**, un composant générique, et `<ErrorBoundary>`. Le fil rouge se dote d'`AsyncBoundary`, composant réutilisable :

```razor
@typeparam T

<ErrorBoundary>
       <ChildContent>
              @if (_loading)
              {
                     @if (Fallback is not null) { @Fallback }
                     else { <p aria-busy="true">Chargement...</p> }
              }
              else if (_value is not null)
              {
                     @ChildContent(_value)
              }
       </ChildContent>
       <ErrorContent Context="ex">
              <p role="alert">Une erreur est survenue : @ex.Message</p>
       </ErrorContent>
</ErrorBoundary>

@code {
       [Parameter, EditorRequired] public Func<Task<T>> Loader { get; set; } = default!;
       [Parameter, EditorRequired] public RenderFragment<T> ChildContent { get; set; } = default!;
       [Parameter] public RenderFragment? Fallback { get; set; }

       private T? _value;
       private bool _loading = true;

       protected override async Task OnInitializedAsync()
       {
              _value = await Loader();
              _loading = false;
       }
}
```

Utilisation : chaque section d'une page peut charger **indépendamment** du reste, avec son propre état de chargement et son propre traitement d'erreur.

> **Piège (vécu lors de la mise en œuvre)** : un gabarit Razor inline `@<p>...` ne peut pas être imbriqué dans une expression entre parenthèses `@(Fallback ?? @<p>...)`. Une seule erreur de syntaxe en amont produit une cascade d'erreurs absurdes en aval (« `T` introuvable ») — chercher toujours la **première** erreur du compilateur.

## 20. Choosing a Strategy

La page du cours qui ferme le module : le choix du mode de rendu **par page**, justifié :

| Page de ShopPulse | Stratégie | Pourquoi |
|---|---|---|
| Accueil, à propos | Static SSR + OutputCache | SEO, rapide, rarement modifié |
| Catalogue | Static SSR + `[StreamRendering]` | SEO + données lentes |
| Fiche produit | Static SSR + îlot panier | SEO + un seul bout interactif |
| Panier, checkout | InteractiveServer | Forte interactivité, pas de SEO |
| Dashboard stock live | InteractiveServer | Mises à jour poussées |
| Back-office | InteractiveServer, `prerender: false` | Authentifié, pas d'indexation |

**Règle de décision** : commencer en Static SSR et ne passer en interactif que lorsque l'interactivité l'exige. Pour chaque page, se poser quatre questions : a-t-elle besoin de SEO ? de données temps réel ? d'état local riche ? de JS interop ?

---

## Bilan du module 1

| Notion | Où elle sera approfondie |
|---|---|
| Output cache, politiques, tags, ISR | Module 5 — Caching |
| Communication entre îlots (CartStore) | Module 3 — State Management |
| Secrets et frontière serveur/client | Module 7 — Security |
| `HeadContent`, canonical et hreflang par langue | Module 8 — i18n |
| JSON-LD construit proprement | Module 7 — Security |
| Mesure du temps d'affichage (LCP) | Module 2 — Performance |

**Les trois choses à retenir :**
1. **Commencer statique, rendre interactif sur besoin.** Chaque circuit SignalR a un coût (mémoire serveur, affinité, latence par interaction) ; les îlots sont la règle, pas l'exception.
2. **Le streaming SSR change l'expérience perçue** : le squelette s'affiche tout de suite, les données lentes arrivent quand elles sont prêtes — sur la même réponse HTTP.
3. **`@key` avec un identifiant métier stable** est non négociable sur les listes ; c'est la différence entre une réconciliation qui réutilise le DOM et une qui le détruit.

## Exercices du module (à faire dans le navigateur, DevTools ouverts)

1. Retirer `[StreamRendering]` de `Catalog.razor`, mesurer le temps d'affichage, puis le remettre et comparer.
2. Ajouter/retirer `@key` sur la liste, filtrer par catégorie, observer le comportement dans l'onglet Elements (flash des nœuds).
3. Remplacer la grille par `<Virtualize>` et comparer le nombre de nœuds DOM.
4. Vérifier dans l'onglet Réseau de la fiche produit qu'un seul circuit (`/_blazor`) s'ouvre.
5. Utiliser `AsyncBoundary` pour une section « Produits similaires » sur la fiche produit.
