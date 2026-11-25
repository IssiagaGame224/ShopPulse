# Chapitre 0 — Étape 0 : Mise en place de ShopPulse

> **Module du cours :** Mise en place (module 0)
> **Fil rouge :** création du projet ShopPulse, socle sur lequel les modules 1 à 8 s'appuient.
> **Prérequis vérifiés :** SDK .NET 10 (10.0.102), Node.js 24, Docker. Navigateur Chromium pour les tests des modules suivants.

---

## 1. Ce qu'est Blazor Server — et pourquoi ce choix change tout

**Concept : Client-Side Rendering (CSR) vs Server-Side Rendering (SSR).**

Dans une application web classique « client-side » (React, Angular, Vue, Blazor WebAssembly), le navigateur télécharge le framework et le code de l'application, puis construit la page lui-même. L'avantage : réactivité. Le coût : un téléchargement initial lourd, un temps avant le premier affichage, et une logique métier exposée au navigateur.

**Blazor Server** inverse le modèle : **aucune logique C# ne s'exécute dans le navigateur**. Le HTML est rendu sur le serveur, et lorsqu'une page devient interactive, le navigateur ouvre une connexion SignalR persistante appelée **circuit**. Chaque interaction utilisateur (clic, saisie) est envoyée au serveur sous forme d'événements ; le serveur recalcule le rendu et renvoie les **différences du DOM** à appliquer. Le navigateur reste un « terminal d'affichage » piloté à distance.

Conséquences directes, que l'on retrouvera dans tout le cours :

- Le premier affichage peut être **instantané** (HTML pur, pas de téléchargement de runtime).
- La latence réseau conditionne la réactivité : chaque interaction fait un aller-retour serveur.
- L'état du composant vit **côté serveur** — c'est la fondation du module 3 (State Management).
- Tout ce qui vient du navigateur est non fiable : c'est la fondation du module 7 (Sécurité).

Le modèle « Blazor Web App » (.NET 8 et suivants) permet de **mélanger** les deux mondes au sein d'une même application : des pages purement statiques (Static SSR) et des composants interactifs, le choix se faisant **par page ou par composant** via `@rendermode`. C'est l'objet du module 1.

## 2. Créer le projet : décortiquer la commande

```bash
dotnet new blazor -n ShopPulse -int Server -au None
```

| Option | Signification | Pourquoi ce choix |
|---|---|---|
| `blazor` | Modèle « Blazor Web App », qui supporte le rendu statique et interactif combinés. | Base du module 1 (CSR vs SSR, Streaming SSR, Hydration). |
| `-n ShopPulse` | Nom de l'application et de l'assembly. | — |
| `-int Server` (`--interactivity Server`) | Le mode interactif par défaut est **Blazor Server** (circuit SignalR). | Aucun C# dans le navigateur ; l'alternative `WebAssembly` sera comparée au module 1. |
| `-au None` (`--auth None`) | Aucune authentification générée. | Le module 7 ajoutera OIDC avec PKCE (Keycloak en développement). |

> **Piège fréquent** : générer le projet avec Individual auth « pour gagner du temps ». On obtient un schéma de sécurité imposé qu'il faudra défaire au module 7. Mieux vaut ajouter l'authentification quand on l'étudie.

## 3. Structure générée et structure cible

Le modèle génère un projet minimal :

```
ShopPulse/
  Components/          # Composants Razor (.razor), Layout/, Pages/
  Program.cs           # Point d'entrée : services + pipeline HTTP
  Properties/          # launchSettings.json (profils de lancement)
  wwwroot/             # Assets statiques (CSS, JS, images)
  appsettings.json
  ShopPulse.csproj
```

Le cours impose dès l'Étape 0 une organisation en couches, même si tout tient dans un seul projet pour l'instant :

```
ShopPulse/
  Domain/              # Modèles métier, aucune dépendance technique
    Product.cs
  Services/            # Abstractions et implémentations d'accès aux données
    IProductService.cs
    InMemoryProductService.cs
  Components/          # UI (le cours y ajoutera Catalog, ProductDetail, ProductCard…)
```

Cette séparation est volontairement exagérée pour un projet si petit : elle prépare le module 4 (Architecture), où chaque feature deviendra une Razor Class Library et où des tests d'architecture (NetArchTest) garantiront que `Domain` ne référence jamais ASP.NET Core. Les bonnes frontières ne coûtent rien quand on les pose dès le premier jour.

## 4. Le modèle de domaine : `Product`, un `record`

```csharp
namespace ShopPulse.Domain;

public record Product(
       int Id,
       string Name,
       string Category,
       decimal Price,
       int Stock,
       string ImageUrl,
       string Description);
```

**Concept : l'immuabilité et la valeur sémantique des `record`.**

Un `record` en C# est un type référence dont l'égalité se fait **par valeur** (deux produits avec les mêmes champs sont « le même » produit), et dont les instances sont conçues pour être **immuables** : on ne modifie pas un produit, on en crée une copie transformée grâce à l'expression `with`.

Pourquoi c'est important côté frontend :

- L'immuabilité rend les comparaisons fiables : Blazor peut décider efficacement de ce qui a changé et de ce qui doit être réaffiché (on retrouvera cette idée au module 1, concept « Virtual DOM », et au module 3, concept « Immutability »).
- Un modèle de domaine sans dépendance technique peut voyager entre projets : c'est ce qui permettra au module 6 de partager les DTO entre l'API et le front sans doublon.

## 5. Programmer contre une interface : `IProductService`

```csharp
public interface IProductService
{
       Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default);
       Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
}
```

**Concept : l'inversion de dépendance (DIP).** Les composants de l'UI dépendront de l'abstraction `IProductService`, jamais de `InMemoryProductService`. L'implémentation pourra donc changer — base de données réelle au module 3, API HTTP séparée au module 6 — sans toucher une seule ligne des composants.

Deux détails qui annoncent les modules à venir :

- **`CancellationToken ct = default` sur chaque méthode asynchrone.** Tout appel réseau ou coûteux doit être annulable (changement de page, délai dépassé). C'est la première brique du module 6 (Networking : annulation, reprise, délais).
- **`Task<IReadOnlyList<Product>>`** : on expose une liste en lecture seule. L'appelant ne peut pas modifier le résultat derrière le dos du service — un pas vers l'immuabilité des frontières.

## 6. L'implémentation en mémoire et la latence simulée

```csharp
public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
{
       await Task.Delay(1500, ct);  // latence simulée
       return _data;
}
```

**Concept : pourquoi simuler de la lenteur volontairement.**

Les 200 produits sont générés par `Enumerable.Range(1, 200).Select(...)`, et le `Task.Delay(1500)` simule une base de données distante. Ce choix, qui semble absurde en développement, est en réalité un outil pédagogique : au module 1 (Streaming SSR), cette latence rendra **visible** la différence entre une page qui attend toutes ses données avant de s'afficher et une page qui s'affiche au fur et à mesure. Un service instantané rendrait le streaming imperceptible — on ne verrait rien.

> **Limite** : une liste statique en mémoire n'est pas thread-safe à modifier. Ici elle est créée une fois et jamais modifiée, donc le `static readonly` est sûr. Dès que les données deviendront modifiables (stock), cette implémentation disparaîtra au profit de l'API.

## 7. L'injection de dépendances : premier contact avec les lifetimes

Dans `Program.cs` :

```csharp
builder.Services.AddSingleton<IProductService, InMemoryProductService>();
```

**Concept : les durées de vie (lifetimes) en injection de dépendances.**

ASP.NET Core gère trois durées de vie, dont deux suffisent pour comprendre la règle fondamentale :

| Lifetime | Une instance par… | Usage |
|---|---|---|
| **Singleton** | Toute l'application, tous les utilisateurs confondus. | Services sans état utilisateur, caches globaux, horloges. |
| **Scoped** | Un utilisateur / un circuit (créée à l'ouverture, détruite à la fermeture). | État utilisateur : panier, favoris, formulaires. |
| **Transient** | Chaque injection. | Services légers et sans état, ex. fabriques. |

**La règle d'or du cours (module 3) : un Singleton ne porte jamais d'état utilisateur.** Si le panier était enregistré en Singleton, tous les visiteurs partageraient le même panier ! Ici, `InMemoryProductService` en Singleton est justifié : son « état » (la liste des produits) est identique pour tout le monde et ne varie jamais.

> **Piège** : enregistrer un service avec état en Singleton « parce que ça marche en test solo ». Le bug n'apparaît qu'avec deux navigateurs connectés en même temps — et il est alors invisible en débogage local. Les lifetimes sont un choix d'architecture, pas un détail d'implémentation.

## 8. Le pipeline HTTP dans `Program.cs`

Le fichier généré, enrichi selon le cours :

```csharp
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();                      // HTTP Strict Transport Security
}

app.UseHttpsRedirection();
app.UseAntiforgery();                   // protection anti-falsification de requêtes
app.UseOutputCache();                   // aperçu du module 5 (Caching)
app.MapStaticAssets();                  // assets statiques avec fingerprints

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();   // les composants interactifs utilisent le circuit SignalR

app.Run();
```

**Concept : le pipeline de middlewares.** Chaque requête HTTP traverse les middlewares **dans l'ordre d'enregistrement** : redirection HTTPS, antiforgery, cache de sortie, puis le routage vers les composants Razor. Chaque `UseXxx()` intercepte la requête et peut la traiter, la raccourcir (ex. servir depuis le cache) ou la transmettre au suivant.

Points à retenir dès maintenant :

- **`UseAntiforgery()`** est positionné avant le mapping : toute soumission de formulaire sera protégée par jeton antiforgery. Le module 7 détaillera le mécanisme.
- **`UseOutputCache()`** ajoute le middleware de cache de réponses HTTP — présent dès l'Étape 0, exploité au module 5. Noter que l'enregistrement (`AddOutputCache()`) se fait dans la phase *services*, l'activation (`UseOutputCache()`) dans la phase *pipeline* : **l'injection de dépendances et le pipeline sont deux registres distincts**, c'est une source classique d'erreurs.
- **`MapStaticAssets()`** (nouveauté .NET 9+) sert les fichiers de `wwwroot` avec des noms à empreinte pour un cache navigateur agressif et sûr.

## 9. Vérification : compiler, lancer, observer

```bash
dotnet build     # compilation : 0 erreur, 0 avertissement
dotnet run       # démarrage : "Now listening on: http://localhost:5141"
```

Un appel HTTP à la racine renvoie `200` avec la page d'accueil du modèle (« Hello, world! »). L'application démarre, les services sont enregistrés, le projet est sain. Le catalogue lui-même n'existe pas encore : les composants `Catalog.razor`, `ProductCard.razor`, `ProductDetail.razor` et `AsyncBoundary.razor` sont l'objet du module 1.

> **Remarque** : l'URL et le port proviennent de `Properties/launchSettings.json`. Ne jamais coder d'URL en dur dans l'application — c'est la configuration qui détermine l'adressage.

## Bilan de l'Étape 0

| Notion abordée | Où elle sera approfondie |
|---|---|
| Blazor Server, circuit SignalR, Static SSR vs interactif | Module 1 — Rendering (concepts 1 à 20) |
| CSR vs SSR | Module 1, concept 1 |
| Immuabilité, `record` | Module 1 (Virtual DOM) et module 3 (Immutability) |
| Inversion de dépendance, `CancellationToken` | Module 6 — Networking |
| Lifetimes DI, Singleton vs Scoped | Module 3 — State Management |
| Pipeline de middlewares, antiforgery | Module 7 — Security |
| OutputCache | Module 5 — Caching |
| Organisation Domain / Services / Components | Module 4 — Architecture |

**Les trois choses à retenir :**
1. Blazor Server rend le HTML côté serveur et synchronise le DOM via un circuit SignalR : l'état vit côté serveur.
2. On dépend d'abstractions (`IProductService`), et les lifetimes DI sont un choix d'architecture (Singleton = jamais d'état utilisateur).
3. Le pipeline HTTP (`Program.cs`) et l'injection de dépendances sont deux registres distincts, enregistrés séparément et dans un ordre qui compte.

**Prochaine étape :** Module 1 — Rendering. Le catalogue prend vie, et la latence simulée de 1,5 s va enfin nous permettre de voir le streaming SSR.
