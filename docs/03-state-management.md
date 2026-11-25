# Chapitre 3 — Module 3 : State Management (la gestion de l'état)

> **Question du module :** où vit chaque donnée, qui peut la modifier, et comment l'interface reste-t-elle cohérente avec elle ?
> **Fil rouge :** panier persistant synchronisé entre onglets, catalogue filtré par l'URL, état normalisé, formulaire de livraison.

## Ce que Blazor Server change

Dans une SPA JavaScript, l'état vit dans le navigateur. En Blazor Server, il vit dans la **mémoire du serveur**, rattaché à un **circuit** (environ un onglet ouvert). C'est la durée de vie des services qui décide qui partage quoi :

| Durée de vie | Portée en mode interactif | Portée en Static SSR | Usage |
|---|---|---|---|
| Singleton | Toute l'application, tous les utilisateurs | Idem | Caches partagés, notifications |
| Scoped | Un circuit (environ un onglet) | Une requête HTTP | Panier, thème, état UI |
| Transient | Une instance par injection | Idem | Services sans état |

```csharp
// DANGER : tous les utilisateurs partageraient le même panier
builder.Services.AddSingleton<CartStore>();

// Correct : un panier par circuit
builder.Services.AddScoped<CartStore>();
```

Deux conséquences pour tout le module :

1. Un état en mémoire **disparaît quand le circuit est perdu** (rechargement de la page, reconnexion échouée, redéploiement). Il faut donc le persister (concepts 18 et 19).
2. Plusieurs onglets = plusieurs circuits = plusieurs états indépendants, à synchroniser explicitement.

Les îlots interactifs d'une même page partagent normalement le même circuit, donc le même scope — point à vérifier dans sa version de .NET en observant l'onglet WebSocket (exercice du module 1).

## Socle commun du fil rouge

Trois petites briques réutilisables, dans `State/` :

```csharp
// IObservableState.cs : le contrat minimal
public interface IObservableState
{
       event Action? Changed;
}
```

```csharp
// ObserverComponent.cs : classe de base qui s'abonne et se DÉSABONNE automatiquement
public abstract class ObserverComponent : ComponentBase, IDisposable
{
       private readonly List<IObservableState> _watched = [];

       protected void Watch(IObservableState state)
       {
              state.Changed += OnChanged;
              _watched.Add(state);
       }

       private void OnChanged() => _ = InvokeAsync(StateHasChanged);

       public virtual void Dispose()
       {
              foreach (var s in _watched) s.Changed -= OnChanged;
       }
}
```

Le désabonnement est vital : un service Scoped vit plus longtemps que le composant. Sans `-=`, chaque navigation laisse un composant « fantôme » en mémoire, qui continue de recevoir des événements. `InvokeAsync(StateHasChanged)` est obligatoire dès qu'un changement peut arriver hors du contexte de synchronisation du composant (timer, événement d'un autre circuit, callback JS).

```csharp
// Store.cs : store minimaliste (style Zustand) — état immuable + méthodes
public abstract class Store<TState> : IObservableState where TState : notnull
{
       protected Store(TState initial) => State = initial;

       public TState State { get; private set; }
       public event Action? Changed;

       /// <summary>Applique une transformation pure. Ne notifie que si l'état a changé.</summary>
       protected void Set(Func<TState, TState> update)
       {
              var next = update(State);
              if (EqualityComparer<TState>.Default.Equals(next, State)) return;
              State = next;
              Changed?.Invoke();
       }
}
```

## 1. Local vs Global State

Règle : **l'état vit au plus bas niveau qui suffit.**

| Type | Exemple ShopPulse | Où |
|---|---|---|
| Local | Quantité en cours de saisie, menu ouvert | Champ du composant |
| Partagé (parent/enfants) | Onglet actif d'une fiche produit | Parent + paramètres |
| Global (circuit) | Panier, thème, utilisateur | Service Scoped |
| Serveur | Produits, stock | Base de données + cache |
| URL | Catégorie, page, recherche | Query string |

Un compteur de quantité est un champ privé avec un `EventCallback` vers le parent ; un panier global est justifié, la quantité d'un champ de saisie ne l'est pas. Ne remontez l'état que quand deux composants éloignés en ont besoin.

## 2. Context API

L'équivalent React est `CascadingValue` / `[CascadingParameter]` : une valeur offerte à tous les descendants sans la passer de parent en enfant.

```razor
<CascadingValue Value="_theme" IsFixed="true"> … </CascadingValue>
```

`IsFixed="true"` signifie que la référence ne changera jamais : Blazor n'a pas besoin de suivre les abonnés, ce qui est plus rapide. Sans lui, un changement de valeur re-rend tous les consommateurs.

> **Limite importante** : une `CascadingValue` ne traverse pas la frontière Static SSR → composant interactif. Pour une valeur disponible partout, y compris dans des îlots, on l'enregistre au niveau racine : `builder.Services.AddCascadingValue(sp => new ThemeState());`. Pour un état partagé entre îlots, un service Scoped (concept 4) reste le choix le plus fiable.

## 3. Redux Toolkit

Le modèle Redux : état unique et immuable, actions décrivant ce qui s'est passé, reducer pur `(état, action) → nouvel état`. Le cours en donne l'implémentation complète (`CartRedux.cs` : `CartState`, actions `ItemAdded`/`ItemRemoved`/`CartCleared`, `CartReducer.Reduce`, et un `ReduxStore<TState, TAction>` avec hook de journalisation `OnDispatched` — l'équivalent d'un middleware).

Correspondances : `createSlice` → reducer + records d'actions ; `createAsyncThunk` → méthode async qui dispatche avant et après ; `createEntityAdapter` → `EntityState<T>` (concept 7) ; RTK Query → `QueryClient` (concept 6) ; Redux DevTools → **Fluxor** (bibliothèque NuGet).

> **Décision du fil rouge** : le cours autorise à ne garder qu'une seule implémentation. ShopPulse retient le store style Zustand (concept 4) pour le panier ; le code Redux documenté ici sert à comprendre le principe. Quand choisir Redux ? Quand beaucoup d'endroits modifient le même état et que vous voulez une trace (audit, débogage, annulation). Sinon c'est du code en plus. Pour un vrai projet Redux-like en Blazor : Fluxor, pas un store maison.

## 4. Zustand

Zustand est un store minimaliste : un objet avec un état et des méthodes, sans actions ni reducers. Un service Scoped avec un événement `Changed` joue exactement ce rôle — c'est `Store<TState>` ci-dessus. `CartStore` du fil rouge :

```csharp
public sealed class CartStore : Store<CartState>
{
       public void Add(int productId, int quantity = 1) => Set(s =>
       {
              var i = s.Lines.FindIndex(l => l.ProductId == productId);
              return i < 0
                     ? s with { Lines = s.Lines.Add(new CartLine(productId, quantity)) }
                     : s with { Lines = s.Lines.SetItem(i, s.Lines[i] with { Quantity = s.Lines[i].Quantity + quantity }) };
       });
       // SetQuantity, Remove, Replace, Clear…
}
```

Enregistrement et usage :

```csharp
builder.Services.AddScoped<CartStore>();
```

```razor
@* CartBadge.razor *@
@inherits ObserverComponent
@inject CartStore Cart

<a href="/cart" aria-label="Panier : @Count articles"><span>@Count</span></a>

@code {
       private int Count => Cart.State.ItemCount(); // sélecteur (concept 17)
       protected override void OnInitialized() => Watch(Cart);
}
```

Le `AddToCartButton` des modules 1-2 est enfin branché : `Cart.Add(ProductId)` côté service, et un état **local** (`_justAdded`) pour le retour visuel du libellé. Le panier est global, l'indicateur « Ajouté » est local — la distinction exacte du concept 1.

## 5. Server State

Une donnée est du *server state* quand le serveur en est le propriétaire et que l'UI n'en détient qu'une copie qui peut devenir périmée (produits, stock, prix). Elle se distingue de l'état client par : chargement asynchrone (loading, erreur, succès), caractère partagé, péremption.

L'erreur classique : stocker des données serveur dans un store global sans jamais les rafraîchir. Le fil rouge modélise explicitement les trois états :

```csharp
public abstract record AsyncState<T>
{
       public sealed record Idle : AsyncState<T>;
       public sealed record Loading : AsyncState<T>;
       public sealed record Success(T Value) : AsyncState<T>;
       public sealed record Failure(Exception Error) : AsyncState<T>;
}
```

Avec un `switch` sur le type, **l'état impossible n'est pas représentable** : on ne peut pas avoir à la fois « chargement » et « erreur ».

## 6. TanStack Query

TanStack Query apporte : cache par clé, dédoublonnage des requêtes, péremption (`staleTime`), invalidation. Pas d'équivalent officiel en Blazor → le fil rouge construit un mini `QueryClient` :

```csharp
public async Task<T> GetAsync<T>(string key, Func<CancellationToken, Task<T>> fetch, TimeSpan staleTime)
{
       var entry = _cache.AddOrUpdate(
              key,
              _ => Create(fetch),
              (_, old) => IsUsable(old, staleTime) ? old : Create(fetch));
       return (T)(await entry.Task.Value)!;
}
```

Deux idées fines : le `Lazy<Task<object?>>` garantit **une seule exécution même en concurrence** (deux composants demandant `products:all` en même temps ne déclenchent qu'un appel — exercice 4) ; et la requête partagée tourne avec `CancellationToken.None` — *si le premier appelant abandonnait, tous les suivants recevraient une annulation*.

```csharp
builder.Services.AddScoped<QueryClient>();   // par circuit
```

> **Limites assumées** : pas de retry, pas de rafraîchissement en arrière-plan, pas de nettoyage des entrées. Retry : module 6. Le cache est par circuit ; pour un cache partagé entre utilisateurs : `HybridCache` (module 2, Singleton par nature).

## 7. Normalized State

Au lieu de copier un produit dans le catalogue, le panier et la fiche, on stocke chaque entité **une seule fois, indexée par id** ; les autres endroits ne gardent que l'identifiant.

```csharp
public sealed record EntityState<T>(ImmutableDictionary<int, T> ById, ImmutableList<int> Ids)
{
       public EntityState<T> UpsertMany(IEnumerable<T> items, Func<T, int> key) { /* … */ }
       public IEnumerable<T> All() => Ids.Select(id => ById[id]);
}
```

`ProductStore.Load(products)` alimente le dictionnaire ; `ProductStore.UpdateStock(id, stock)` modifie **un seul endroit**, et le catalogue, la fiche et le panier affichent tous la nouvelle valeur. Le panier ne contient que des `CartLine(ProductId, Quantity)` — jamais de copie de produit.

## 8. Derived State

Une donnée qui se calcule à partir d'une autre ne doit pas être stockée : **deux sources de vérité finissent toujours par diverger**.

```csharp
// Mauvais : _total doit être mis à jour partout où le panier change
private decimal _total;

// Bon : calculé à la demande
private decimal Total => Cart.State.Total(Catalog.State);
```

Pour un calcul coûteux, le memoïser par comparaison de références : si `Cart.State` et `Catalog.State` n'ont pas changé de référence (immuabilité !), le total précédent est encore valide. Cela fonctionne grâce au concept 9.

## 9. Immutability

Un état immuable ne se modifie jamais : on en crée une nouvelle version. Avantages : détection de changement par simple comparaison de références, pas de modification accidentelle par un autre composant, historique et annulation possibles. Outils C# : `record` + `with`, `System.Collections.Immutable`, exposition en lecture seule (`IReadOnlyList<T>`).

> **Piège** : un record n'est immuable qu'à moitié si l'une de ses propriétés est mutable.

```csharp
public record Bad(List<int> Ids);            // Ids.Add(...) reste possible
public record Good(ImmutableList<int> Ids);  // vraiment immuable

var a = new Bad([1]);
var b = a with { };
b.Ids.Add(2);        // b.Ids et a.Ids sont LA MÊME liste : modifie aussi a !
```

Blazor ajoute une raison de plus : l'immuabilité des paramètres permet à un enfant d'éviter un re-rendu (`ShouldRender`, module 2).

## 10. Optimistic Updates

Mettre à jour l'UI **avant** la réponse du serveur, puis annuler si l'opération échoue. Idéal pour les actions fréquentes et presque toujours réussies (favoris, « j'aime »). En Blazor Server, cela masque la latence de l'aller-retour SignalR.

```csharp
public async Task ToggleAsync(int id)
{
       var target = !IsFavorite(id);
       Apply(id, target);                    // 1. mise à jour optimiste
       try
       {
              await api.SetAsync(id, target); // 2. confirmation serveur
       }
       catch (Exception)
       {
              Apply(id, !target);             // 3. annulation de CETTE modification seulement
              Failed?.Invoke("Impossible de mettre à jour vos favoris.");
       }
}
```

On annule par **l'opération inverse** plutôt qu'en restaurant un instantané complet : si l'utilisateur a cliqué sur un autre favori entre-temps, restaurer l'ancien état écraserait aussi cette action. N'utilisez pas l'optimisme pour les actions à enjeu (paiement, suppression définitive).

## 11. Cache Invalidation

Quand les données changent, les copies en cache doivent disparaître — aux **trois niveaux ensemble** :

```csharp
public async Task UpdateProductAsync(Product p)
{
       await _repo.SaveAsync(p);
       await _hybridCache.RemoveByTagAsync("catalog");         // cache partagé serveur (module 2)
       await _outputCache.EvictByTagAsync("catalog", default); // pages en cache (module 1)
       _query.Invalidate("products");                          // cache du circuit courant
}
```

Les **autres circuits** ne savent pas qu'un produit a changé. Un service Singleton peut les prévenir :

```csharp
public sealed class CatalogNotifier       // SEUL Singleton du module : un bus, aucun état utilisateur
{
       public event Action<int>? ProductChanged;
       public void Publish(int productId) => ProductChanged?.Invoke(productId);
}
```

Le composant abonné invalide son `QueryClient` et recharge. Le `-=` est encore plus critique ici : un Singleton garde les composants abonnés de **tous les circuits vivants** — un oubli est une fuite mémoire serveur. Le module 6 remplacera ce mécanisme par un hub SignalR.

## 12. Form State

Un formulaire a son propre état : valeurs, erreurs, champs modifiés, envoi en cours. Blazor fournit `EditForm` + `EditContext` + DataAnnotations (`Domain/ShippingForm.cs` avec `[Required]`, `[EmailAddress]`, `[RegularExpression]`…, et `Components/Shared/ShippingFormView.razor`).

Remarques du cours :

- Avec `EditContext="..."`, on ne passe pas aussi `Model`. Les deux sont exclusifs.
- Pour un formulaire en Static SSR (sans circuit) : `Model` avec `[SupplyParameterFromForm]` et un `FormName`. `EditForm` insère alors le jeton antiforgery ; un `<form>` HTML pur doit contenir `<AntiforgeryToken />` à la main.
- `EditContext.OnFieldChanged` permet de suivre les champs modifiés — et exige un `-=` dans `Dispose`.
- La validation côté serveur reste **obligatoire** même avec Blazor Server : les attributs protègent l'UI, pas votre domaine (module 7).

## 13. URL as State

Les filtres, le tri et la pagination doivent vivre dans l'URL : le lien est partageable, le bouton « précédent » marche, la page est indexable, l'état survit au rechargement et à la perte du circuit.

```razor
@page "/catalog"
[SupplyParameterFromQuery] public string? Category { get; set; }
[SupplyParameterFromQuery(Name = "q")] public string? Search { get; set; }
[SupplyParameterFromQuery(Name = "page")] public int? PageParam { get; set; }

protected override async Task OnParametersSetAsync() { /* filtrer + paginer */ }

private string PageUrl(int page) => Nav.GetUriWithQueryParameter("page", page);
```

Points clés : `OnParametersSetAsync` (et non `OnInitializedAsync`) se redéclenche quand la query string change ; `GetUriWithQueryParameter` conserve les autres paramètres ; en interactif, `Nav.NavigateTo(…, replace: true)` change l'URL sans polluer l'historique. La page fonctionne **sans circuit** : le formulaire est un simple GET — c'est la forme ultime de la règle « commencer statique ».

**Dans le fil rouge**, cette page fusionne les acquis des modules 1 et 2 : filtres dans l'URL *et* squelettes anti-CLS *et* images priorisées LCP. Vérifié : `/catalog?category=Audio&q=Produit&page=2` sert « Page 2 / 6 » avec la catégorie présélectionnée.

## 14. State Machines

Quand un flux a des étapes et des transitions autorisées, une machine à états rend les états impossibles impossibles. Le tunnel de commande du fil rouge (`CheckoutMachine`) :

```csharp
private static readonly Dictionary<(CheckoutStep, CheckoutEvent), CheckoutStep> Transitions = new()
{
       [(Cart,     Next)]  = Shipping,
       [(Shipping, Back)]  = Cart,
       [(Payment,  Pay)]   = Confirmed,
       [(Payment,  PaymentFailed)] = Failed,
       [(Failed,   Retry)] = Payment,
       // …
};

public void Fire(CheckoutEvent e)
{
       if (!Transitions.TryGetValue((Step, e), out var next))
              throw new InvalidOperationException($"« {e} » est interdit depuis l'étape « {Step} ».");
       Step = next;
       Changed?.Invoke();
}
```

L'UI se pilote sur `CanFire` : les boutons s'activent selon les transitions autorisées, donc l'UI ne peut pas proposer une action interdite. Pour des machines riches (états hiérarchiques, gardes) : la bibliothèque **Stateless**.

## 15. Signals

Les signals (Solid, Angular, Vue) sont des valeurs réactives qui notifient leurs dépendants. Blazor n'a pas de signals natifs, et surtout pas de mise à jour fine du DOM : un changement re-rend le composant, puis Blazor compare l'arbre (module 1, concept 12). Le fil rouge reproduit quand même le modèle d'abonnement (`Signal<T>` + `Computed<T>` dans `State/Signals.cs`).

Écarts avec de vrais signals : dépendances déclarées à la main (pas de suivi automatique), aucun gain de granularité de rendu. Dans la plupart des cas, un champ privé et un `Store<T>` suffisent — ce concept sert surtout à comprendre le modèle.

## 16. Avoiding Prop Drilling

Passer une valeur à travers des composants qui ne l'utilisent pas. Trois remèdes, du plus léger au plus puissant :

| Situation | Outil |
|---|---|
| État de l'application (panier, utilisateur) | Service Scoped injecté |
| Contexte d'une zone (thème, formulaire, onglets) | `CascadingValue` |
| Un parent personnalise le rendu d'un enfant | `RenderFragment<T>` |
| Deux composants voisins | Paramètres + `EventCallback` |

```razor
@* [NON] Drilling : Page → Grid → Card → Button, seul Button a besoin du panier *@
<Grid Cart="Cart"> <Card Cart="Cart"> <Button Cart="Cart" /> …

@* [OUI] Injection : le composant prend ce dont il a besoin *@
@inject CartStore Cart
```

L'injection ne doit pas devenir un prétexte pour tout mettre en global : l'état proche de son usage reste plus facile à comprendre.

## 17. Selectors

Un sélecteur est une **fonction pure** qui extrait ou calcule une valeur à partir de l'état — centralisée et testable sans Blazor :

```csharp
public static class CartSelectors
{
       public static int ItemCount(this CartState cart) => cart.Lines.Sum(l => l.Quantity);
       public static decimal Total(this CartState cart, EntityState<Product> catalog) => /* … */;
       public static bool HasOutOfStock(this CartState cart, EntityState<Product> catalog) => /* … */;
       public static IEnumerable<Product> InCategory(this EntityState<Product> catalog, string? category) => /* … */;
}
```

## 18. Persisting State

Le panier ne doit pas disparaître quand le circuit se ferme. `ProtectedLocalStorage` écrit dans le localStorage **en chiffrant** via ASP.NET Core Data Protection ; il ne fonctionne qu'en mode interactif, après le premier rendu (jamais pendant le prérendu).

| Besoin | Outil |
|---|---|
| Utilisateur anonyme, même navigateur | ProtectedLocalStorage |
| Session de l'onglet seulement | ProtectedSessionStorage |
| Utilisateur connecté, multi-appareils | Base de données (le choix sérieux pour un panier) |
| Passer l'état du préreendu au circuit | PersistentComponentState (module 1) |

Les données chiffrées ne sont pas lisibles par l'utilisateur, mais ne mettez pas de secrets dans le stockage navigateur. En déploiement multi-serveurs, partagez le key ring de Data Protection, sinon un serveur ne pourra pas déchiffrer ce qu'un autre a écrit.

## 19. Sync Across Tabs

Chaque onglet a son propre circuit, donc son propre `CartStore`. Pour les synchroniser, on passe par le navigateur : **BroadcastChannel** prévient les autres onglets, qui rechargent l'état depuis le stockage (`wwwroot/js/tabSync.js`).

`CartSync.razor` est un **composant invisible** placé une fois dans le layout, comme îlot interactif :

```razor
<CartSync @rendermode="InteractiveServer" />
<CartBadge @rendermode="InteractiveServer" />
```

Le flux complet, dans l'ordre : `OnAfterRenderAsync` (premier rendu) → `LoadAsync()` restaure depuis `ProtectedLocalStorage` → abonnement `Cart.Changed` → import du module JS + `DotNetObjectReference` → à chaque modification locale : **d'abord écrire le stockage, puis publier** → l'onglet distant reçoit `OnRemoteChange` → `LoadAsync()` recharge. Le drapeau `_applyingExternal` évite de réécrire ce qu'on vient de lire (boucle infinie sinon).

> **Limites** : écritures rapprochées = « dernier écrit gagne » ; pour un vrai panier multi-appareils, la base de données est la source de vérité et la notification devient SignalR (module 6).

## 20. Choosing a Store

| Besoin | Choix | Pourquoi |
|---|---|---|
| Valeur utilisée par un seul composant | Champ privé | Aucun mécanisme nécessaire |
| Parent et enfants proches | Paramètres + EventCallback | Flux explicite |
| Zone de l'arbre (thème, formulaire) | CascadingValue | Évite le drilling |
| État client partagé (panier, favoris) | Store<T> Scoped (style Zustand) | Simple, testable |
| Beaucoup d'acteurs, besoin d'historique/audit | Reducer + Fluxor | Traçabilité |
| Données serveur avec cache | QueryClient / HybridCache | Péremption, dédoublonnage |
| Filtres, tri, page | URL | Partage, SEO, rechargement |
| Flux avec étapes strictes | Machine à états | Transitions explicites |
| Doit survivre à la perte du circuit | Stockage navigateur ou base | La mémoire serveur est éphémère |

Règle de décision : commencer par le champ local, monter d'un cran seulement quand un vrai problème apparaît (partage, péremption, persistance). **Le plus gros piège de Blazor Server reste le Singleton avec état utilisateur.**

---

## Récapitulatif : ShopPulse après le module 3

Enregistrements dans `Program.cs` : les cinq stores et `QueryClient` en Scoped, `IFavoriteService` en Scoped, `CatalogNotifier` **seul Singleton** (`AddCascadingValue(sp => new ThemeState())` pour le thème).

| Fonction | Où vit son état |
|---|---|
| Filtres, page | URL |
| Liste de produits | QueryClient (+ ProductStore normalisé) |
| Panier | CartStore (Scoped) + stockage navigateur |
| Favoris | FavoritesStore + mise à jour optimiste |
| Tunnel de commande | CheckoutMachine |
| Formulaire de livraison | EditContext |
| Thème | CascadingValue |

**Les trois choses à retenir :**
1. **La durée de vie du service décide de qui partage quoi.** Scoped par défaut pour tout état utilisateur ; un Singleton ne porte jamais d'état utilisateur.
2. **Immuabilité + références** : détection de changement gratuite, memoization par comparaison de références, annulations propres. Un `record` avec une `List` mutable est un piège à moitié ouvert.
3. **Tout cache est une promesse d'invalidation** (règle d'or n° 2) : trois niveaux à invalider ensemble, et les autres circuits ont besoin d'un bus (Notifier aujourd'hui, SignalR au module 6).

## Exercices du module

1. Prouver le danger du Singleton : enregistrer `CartStore` en Singleton, ouvrir deux navigateurs, ajouter un article dans l'un, constater dans l'autre. Remettre Scoped.
2. Fuite d'abonnement : retirer `Dispose` d'`ObserverComponent`, naviguer 20 fois, observer les handlers sur `CartStore.Changed`.
3. Normalisation : appeler `ProductStore.UpdateStock` depuis un bouton de test et vérifier que catalogue, fiche et panier changent ensemble.
4. Dédoublonnage : deux `Query.GetAsync("products:all", …)` en parallèle (`Task.WhenAll`) → un seul appel au service (compteur).
5. Optimiste : régler `InMemoryFavoriteService.FailureRate = 0.33` (prévu dans le fil rouge) et observer l'annulation une fois sur trois.
6. Synchronisation : deux onglets, ajouter un produit dans le premier, vérifier que le badge du second se met à jour sans rechargement.
7. URL : copier l'URL d'un catalogue filtré dans une fenêtre privée — le filtre doit être identique.
8. Machine à états : test unitaire vérifiant que `Fire(Pay)` depuis `Cart` lève une `InvalidOperationException`.
