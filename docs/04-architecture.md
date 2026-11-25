# Chapitre 4 — Module 4 : Architecture

> **Question du module :** comment organiser le code pour qu'une équipe puisse le faire grandir sans le casser ?
> **Fil rouge :** ShopPulse passe d'un projet unique à un **monolithe modulaire** — un seul déploiement, des frontières nettes vérifiées par le compilateur et par des tests.
> **Opération majeure :** `ShopPulse/` (projet unique) est remplacé par la solution `ShopPulse.slnx`. Le serveur de développement écoute désormais sur **http://localhost:5026**.

## Architecture cible

```
ShopPulse.slnx
Directory.Build.props        # net10.0, Nullable/ImplicitUsings, TreatWarningsAsErrors, LangVersion 14
Directory.Packages.props     # versions centralisées (CPM)
src/
  ShopPulse.Domain/          # entités, règles métier (Product, Pricing, ShippingForm) — aucune dépendance
  ShopPulse.SharedKernel/    # Result<T>, IClock, socle d'état (Store, QueryClient, EntityState…),
                             # CartState/CartLine, ICartGateway
  ShopPulse.Application/     # ports : IProductService, PagedResult<T>, IFavoriteService
  ShopPulse.Infrastructure/  # adaptateurs : InMemoryProductService, CachedProductService (HybridCache),
                             # InMemoryFavoriteService, CatalogNotifier
  ShopPulse.Modularity/      # IShopModule, ModuleCatalog, PluginLoader
  ShopPulse.UI/              # design system (RCL) : jetons CSS, Button, ProductCard, AddToCartButton,
                             # AsyncBoundary, ObserverComponent, styleguide
  ShopPulse.Features.Catalog/    # RCL : catalogue, fiche produit, ProductStore, FavoritesStore
  ShopPulse.Features.Cart/       # RCL : CartStore, CartPage, CartBadge, CartSync
  ShopPulse.Features.Checkout/   # RCL : CheckoutMachine, tunnel, ShippingFormView
  ShopPulse.Web/             # hôte Blazor : compose les modules, layout, Program.cs
tests/
  ShopPulse.Architecture.Tests/  # règles de dépendances (NetArchTest.Rules)
```

Le format `.slnx` est celui par défaut des modèles .NET 10 (`dotnet new sln` le crée directement). Les commandes exactes de création sont celles du cours (concept « Architecture cible »).

## 1. Component Design

Un bon composant a **une responsabilité, une API claire** (paramètres typés) et ne connaît pas son contexte :

| Type | Rôle | Exemple |
|---|---|---|
| Présentation (« bête ») | Affiche ce qu'on lui donne, émet des événements | `ProductCard` |
| Conteneur (« intelligent ») | Charge les données, orchestre | `Catalog` (page) |
| Mise en page | Organise des zones | `MainLayout`, grilles |

```razor
@* ShopPulse.UI/Components/Organisms/ProductCard.razor : aucun service injecté *@
<article class="card">
       <h3>@Name</h3>
       <p class="price">@Price.ToString("C")</p>
       @ChildContent
</article>
```

Principes appliqués dans le fil rouge : **composition plutôt qu'héritage** (`ChildContent`, `RenderFragment<T>`), `[EditorRequired]` pour les paramètres obligatoires, `[Parameter(CaptureUnmatchedValues = true)]` pour laisser passer `class`, `aria-*` et tout attribut non prévu (voir `Button.razor`).

> **Arbitrage documenté** : la version UI de `ProductCard` remplace celle des modules 1-3 (qui prenait un `Product` entier). La nouvelle API est purement présentationnelle — les acquis des modules 1-2 (priorité LCP, `aspect-ratio`) sont conservés via les paramètres `ImageUrl`/`Priority`.

## 2. Design Systems

Un design system = jetons (couleurs, espacements, typographie) + composants + règles d'usage, publié comme bibliothèque partagée. Dans ShopPulse, c'est la RCL **ShopPulse.UI** :

```css
/* ShopPulse.UI/wwwroot/tokens.css */
:root {
   --sp-color-primary: #0b5fff;
   --sp-space-4: 1rem;
   --sp-radius: .5rem;
}
```

Les fichiers statiques d'une RCL sont servis sous `_content/ShopPulse.UI/tokens.css` et référencés dans `App.razor` via `@Assets["_content/ShopPulse.UI/tokens.css"]`. **Vérifié : le jeton CSS et le JS de la feature Cart sont bien servis sous `_content/` dans le fil rouge.**

## 3. Atomic Design

Cinq niveaux (Brad Frost) : **atomes** (bouton, champ), **molécules** (champ + libellé + erreur), **organismes** (carte produit avec actions), **gabarits**, **pages**. Structure de `ShopPulse.UI/Components/` : `Atoms/Button`, `Organisms/ProductCard`, `Organisms/AddToCartButton`, `Pages/Styleguide`…

> **Règle d'esprit** : ne soyez pas dogmatique — si le classement d'un composant vous fait hésiter plus de dix secondes, ce n'est pas utile. L'intérêt concret : repérer ce qui est réutilisable, et bloquer les dépendances de bas en haut (un atome ne connaît jamais une molécule).

## 4. Monorepos

Tout le code dans un dépôt : refactorisations transversales atomiques, une seule version des dépendances, CI unique. En .NET, un monorepo se pilote avec des fichiers à la racine :

- `Directory.Build.props` : `net10.0`, `Nullable`/`ImplicitUsings`, **`TreatWarningsAsErrors=true`**, `LangVersion 14` — appliqué à tous les projets.
- `Directory.Packages.props` : **versions centralisées** (`ManagePackageVersionsCentrally`) — un seul endroit pour les versions de paquets.

> **Piège vécu lors de la migration** : avec la gestion centralisée, tout `PackageReference` qui épingle une version (les modèles RCL et xunit le font) fait échouer la restauration (NU1008). Il faut déplacer chaque version vers un `PackageVersion` central. Avantage immédiat : `TreatWarningsAsErrors` a transformé un avertissement de paquet redondant (NU1510) en erreur de build — le monorepo intercepte ce genre de dérive dès la compilation.

Inconvénients : CI à optimiser (ne tester que ce qui a changé), droits d'accès plus larges.

## 5. Micro Frontends

L'idée : plusieurs équipes déploient indépendamment des morceaux d'une même interface.

| Approche | Indépendance de déploiement | Complexité |
|---|---|---|
| **Monolithe modulaire (RCL)** | Faible : un seul déploiement | **Faible : recommandé** |
| Applications séparées + liens / reverse proxy (YARP) | Forte | Moyenne |
| iframes | Forte | Élevée (communication, accessibilité) |
| Web Components / JS | Forte | Élevée |

Pour 90 % des équipes, le monolithe modulaire donne la plupart des bénéfices (frontières, propriété) sans le coût opérationnel. Réservez les micro-frontends à des équipes réellement autonomes, avec cycles de livraison différents. **C'est la décision de l'ADR-004** (`docs/adr/ADR-004-monolithe-modulaire.md`).

## 6. Module Federation

Module Federation (Webpack/Vite) charge du code d'une autre application à l'exécution. **Pas d'équivalent direct en Blazor Server** : le C# est compilé et exécuté sur le serveur. Équivalents les plus proches :

- **Chargement dynamique d'assemblies** (plugins, concept 10) : le serveur charge des DLL au démarrage.
- **Composition au niveau HTTP** : YARP, ou inclusion de fragments (`<iframe>`, Server-Side Includes).
- **RCL partagées** : même code, même déploiement (le choix de ShopPulse).
- Pour du JS partagé entre applications : une bibliothèque npm versionnée reste la plus simple.

## 7. Folder Structure

Par type technique (`Components/ Services/ Models/`) déconseillé à grande échelle ; **par fonctionnalité** recommandé : chaque feature contient tout ce dont elle a besoin — pour modifier le panier, on ne touche qu'un dossier. Dans ShopPulse, chaque feature est une **RCL** pour que le compilateur impose les frontières, ce qu'un simple dossier ne fait pas.

## 8. Feature Modules

Une feature = une RCL qui expose ses pages et enregistre ses propres services :

```csharp
public interface IShopModule
{
       string Name { get; }
       Assembly UiAssembly { get; }              // pour le routage
       void ConfigureServices(IServiceCollection services);
}
```

```csharp
// ShopPulse.Features.Cart/CartModule.cs
public sealed class CartModule : IShopModule
{
       public string Name => "Cart";
       public Assembly UiAssembly => typeof(CartModule).Assembly;

       public void ConfigureServices(IServiceCollection services)
       {
              services.AddScoped<CartStore>();
              services.AddScoped<ICartGateway>(sp => sp.GetRequiredService<CartStore>());
       }
}
```

```csharp
// Composition racine (ShopPulse.Web/Program.cs)
IShopModule[] modules = [new CatalogModule(), new CartModule(), new CheckoutModule()];
foreach (var m in modules) m.ConfigureServices(builder.Services);
builder.Services.AddSingleton(new ModuleCatalog(modules));

app.MapRazorComponents<App>()
     .AddInteractiveServerRenderMode()
     .AddAdditionalAssemblies([.. modules.Select(m => m.UiAssembly), typeof(ShopPulse.UI.Marker).Assembly]);
```

> **Arbitrage documenté** : les services transverses (`QueryClient`, le décorateur de cache, `CatalogNotifier`…) restent enregistrés dans l'hôte. Seuls les services **propres à une feature** vivent dans son module — sinon le module devient un second composition root, et le front « qui enregistre quoi » se diffuse.

## 9. Routing

Les pages des RCL sont découvertes via `AddAdditionalAssemblies` côté serveur et `AdditionalAssemblies` du routeur :

```razor
@* ShopPulse.Web/Components/Routes.razor *@
<Router AppAssembly="typeof(Program).Assembly"
            AdditionalAssemblies="[typeof(ShopPulse.UI.Marker).Assembly, .. Catalog.Assemblies]">
```

Bonnes pratiques : contraintes de route (`@page "/product/{Id:int}"`), préfixes par feature (`/catalog/...`, `/cart/...`), routes centralisées dans une classe `Routes` (constantes) pour éviter les chaînes « magiques », `[Authorize]` sur la page (module 7).

## 10. Plugin Architecture

Un plugin est un module découvert au démarrage **sans référence directe** — le serveur charge les assemblies d'un dossier (`PluginLoader.cs` dans `ShopPulse.Modularity`).

> **Piège sécurité** : une DLL chargée s'exécute avec tous les droits du processus. Ne chargez que des assemblies que vous contrôlez (signées, dossier protégé en écriture) ; `AssemblyLoadContext` pour isoler les versions de dépendances. Les pages d'un plugin chargé dynamiquement doivent être ajoutées à `AddAdditionalAssemblies` avant `app.Run()`. (Exercice : créer `ShopPulse.Plugin.Promo` avec une page `/promo`.)

## 11. Shared Libraries

Ce que partagent les modules va dans des bibliothèques stables et peu nombreuses :

| Bibliothèque | Contenu | Règle |
|---|---|---|
| `ShopPulse.SharedKernel` | `Result<T>`, `IClock`, socle d'état, `CartState` | Aucune dépendance métier |
| `ShopPulse.UI` | Design system | Aucune dépendance aux features |
| `ShopPulse.Domain` | Entités | Aucune dépendance |

> **Danger** : le « shared » devient une décharge. Pour y ajouter quelque chose : au moins **deux** modules en ont besoin, et c'est stable.

> **Arbitrage documenté** : `CartSelectors.Total` (qui référence `Product`) vit dans `Features.Cart` et non dans SharedKernel — respect de la règle « aucune dépendance métier ». `ICartGateway` (port d'ajout au panier) vit dans SharedKernel : c'est le contrat qui permet au catalogue d'ajouter au panier **sans référencer la feature Cart** — le mécanisme exact préconisé par le cours pour les échanges entre features.

## 12. Theming

Le thème repose sur les variables CSS du design system : changer de thème, c'est redéfinir les variables (`:root[data-theme="dark"]` et `@media (prefers-color-scheme: dark)` dans `tokens.css`). `ThemeState` (SharedKernel) porte le choix `system | light | dark` en `CascadingValue`.

> **Limite** : le sélecteur de thème et le script anti-flash en ligne dans `<head>` sont l'exercice 5 du cours ; les variables sombles et `ThemeState` sont en place.

## 13. Feature Flags

Activer ou désactiver une fonctionnalité sans redéployer : paquet officiel `Microsoft.FeatureManagement.AspNetCore`, config dans `appsettings.json`, `IFeatureManager.IsEnabledAsync("Reviews")`. Un drapeau est une **dette** : supprimez-le dès que la fonctionnalité est stable, et nommez-le par fonctionnalité, jamais par « test1 ». (Exercice 3 : drapeau `NewCheckout`.)

## 14. Config Driven UI

L'interface décrite par des données (JSON) : navigation, champs, blocs de page. Les éléments masquables par drapeau ne s'affichent que si le flag est actif. `DynamicComponent` pour le rendu dynamique — avec liste blanche de types, jamais de type arbitraire désigné par l'utilisateur. (Exercice : navigation pilotée par la configuration.)

## 15. Separation of Concerns

Chaque couche a un rôle : présentation (Razor), logique d'application (services), domaine (règles), accès aux données. Le contre-exemple du cours — requête SQL, TVA codée en dur et affichage dans le même `@code` — est l'anti-modèle absolu. Dans le fil rouge : `Pricing.WithVat` dans le Domain (testable sans interface ni base), `IProductService` dans Application, `InMemoryProductService`/`CachedProductService` dans Infrastructure, les pages dans les features.

## 16. Clean Architecture

La règle centrale : **les dépendances pointent vers l'intérieur**. Le domaine ne connaît rien ; l'infrastructure connaît le domaine. Le port (interface) est défini côté application ; l'adaptateur côté infrastructure ; la composition (qui branche qui) côté hôte :

```csharp
// Web : la composition
builder.Services.AddSingleton<InMemoryProductService>();
builder.Services.AddSingleton<IProductService>(sp =>
       new CachedProductService(sp.GetRequiredService<InMemoryProductService>(), sp.GetRequiredService<HybridCache>()));
```

L'avantage : on remplace la base de données (ou un service HTTP, module 6) sans toucher aux règles. **Le piège est le sur-ingénierie** : pour un CRUD simple, trois couches suffisent largement.

## 17. Code Ownership

Chaque dossier a des responsables : fichier `.github/CODEOWNERS` imposant les relectures par équipe, associé à la protection de branche. Évite les modifications sans regard d'une équipe sur le code d'une autre. Les décisions importantes vont dans des ADR (concept 20) — le fil rouge contient `docs/adr/ADR-004-monolithe-modulaire.md`.

## 18. Dependency Boundaries

Les frontières ne tiennent que si elles sont **vérifiées automatiquement**. Deux niveaux :

1. **Références de projets** — si `Features.Cart` n'a pas de `ProjectReference` vers `Features.Catalog`, le compilateur refuse l'appel.
2. **Tests NetArchTest** (`tests/ShopPulse.Architecture.Tests`) :

```csharp
[Fact]
public void Domain_ne_depend_de_rien() =>
       Assert.True(Types.InAssembly(typeof(ShopPulse.Domain.Product).Assembly)
              .ShouldNot().HaveDependencyOnAny("ShopPulse.Application", "ShopPulse.Infrastructure",
                     "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
              .GetResult().IsSuccessful);
```

Trois règles dans le fil rouge : Domain isolé, les features ne se connaissent pas, l'UI ne touche pas à l'infrastructure. **`dotnet test` : 3/3 verts.**

> **Exercice du cours** : cassez volontairement une règle (référence ASP.NET dans Domain) pour voir le test rouge — c'est la démonstration que la frontière est réelle.

## 19. Scalability

Trois dimensions : **équipe** (modules, CODEOWNERS), **code** (frontières vérifiées, changements locaux), **charge**. Spécificités Blazor Server :

- Chaque circuit consomme de la mémoire (état des composants + services Scoped) : dimensionnez à partir de **mesures**, pas de chiffres théoriques.
- Une connexion SignalR persistante par utilisateur : prévoyez des limites de connexions.
- **Affinité de session (sticky sessions) nécessaire** derrière un répartiteur, ou Azure SignalR Service (`AddAzureSignalR`).
- Limites configurables : `DisconnectedCircuitMaxRetained`, `DisconnectedCircuitRetentionPeriod`.

Bonne pratique : tout ce qui n'a pas besoin d'interactivité reste en Static SSR — aucun circuit, aucune mémoire d'état. C'est le meilleur levier de passage à l'échelle (règle d'or n° 1, encore elle).

## 20. Documentation

Trois documents, courts et à jour : README de chaque module ; **ADR** (une décision par fichier : statut, contexte, décision, conséquences) ; **catalogue de composants** — la page `/styleguide` de `ShopPulse.UI` montre chaque composant avec ses variantes et sert de test visuel. Le fil rouge l'a implémentée (lien « Styleguide » du menu). Les commentaires XML (`/// <summary>`) donnent l'aide dans l'éditeur.

---

## Récapitulatif : ShopPulse après le module 4

| Élément | Résultat |
|---|---|
| Structure | Monolithe modulaire : Domain, SharedKernel, Application, Infrastructure, Modularity, UI, 3 features, hôte Web |
| Design system | `ShopPulse.UI` avec jetons CSS et composants atomiques |
| Modules | `IShopModule` + `ModuleCatalog`, pages découvertes via `AddAdditionalAssemblies` |
| Garde-fous | NetArchTest (3 tests verts), ADR-004 |
| Vérifié | 8 pages servies (200), assets RCL sous `_content/`, îlot panier fonctionnel |

**Les trois choses à retenir :**
1. **Le compilateur est votre premier garde-frontières** — une RCL par feature, et les références de projets deviennent des règles d'architecture exécutables ; NetArchTest ajoute les règles que les références seules ne vérifient pas.
2. **La composition est un fichier** — tout le câblage vit dans le hôte (`Program.cs`), les modules n'exposent que `ConfigureServices` et leur assembly UI. Qui dépend de qui se lit en un coup d'œil.
3. **Un monolithe modulaire bat des micro-frontends dans 90 % des cas** — mêmes bénéfices d'équipe (propriété, frontières), fraction du coût opérationnel.

## Exercices du module

1. ✅ fait en pair programming : la feature Cart est dans sa propre RCL et `/cart` fonctionne.
2. Faire échouer volontairement un test NetArchTest, observer le rouge, réparer.
3. Drapeau `NewCheckout` (`Microsoft.FeatureManagement.AspNetCore`) : ancienne/nouvelle étape de commande.
4. Plugin `ShopPulse.Plugin.Promo` chargé depuis un dossier, page `/promo`.
5. Thème sombre **sans flash au chargement** (script en ligne dans `<head>`, nonce si CSP — module 7).
6. Écrire l'ADR qui justifie le choix (fait : ADR-004 — à relire et réécrire à votre voix pour votre livre).
