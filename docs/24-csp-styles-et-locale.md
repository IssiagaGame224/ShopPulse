# 24 — CSP : retrait d'`unsafe-inline` sur les styles (+ autopsie d'un bug i18n latent)

> **Contexte.** La CSP appliquée (docs/22) gardait `style-src 'self' 'unsafe-inline'`
> « à resserrer avec des hashes » — héritage du modèle de projet initial. Cette évolution
> réalise le resserrage, ajoute un test de non-régression, et documente deux découvertes
> faites en chemin : un bug i18n latent révélé par l'exécution en conteneur, et un incident
> d'infrastructure Windows sans rapport avec le code.

## L'audit : plus aucun style inline dans l'application

Avant de choisir entre hashes et suppression, un audit complet :

- **aucun** `<style>` ni attribut `style="…"` dans `src/` (recherche exhaustive, hors
  bibliothèques tierces) — la refonte design (docs/16) passe entièrement par les classes
  et les jetons `--sp-*` ;
- les styles posés en JavaScript via `element.style` / `setProperty` (Bootstrap) relèvent
  du **CSSOM**, que `style-src` ne régit pas — seuls les `<style>` et attributs `style`
  du HTML sont concernés ;
- Stripe (Payment Element) vit dans ses iframes, soumises à *leur* CSP, pas à la nôtre.

Conclusion : les hashes prévus étaient inutiles — il n'y a **rien à autoriser**. Le
resserrage est une simple suppression :

```csharp
// avant
"style-src 'self' 'unsafe-inline'; "
// après
"style-src 'self'; "
```

> **Limite** : `style-src 'self'` interdit désormais tout style inline **futur**. C'est
> voulu — un style inline ajouté par mégarde sera bloqué et visible en console, et le
> test E2E ci-dessous échouera.

## Le garde-fou : deux tests E2E permanents

`tests/ShopPulse.E2E/CspE2eTests.cs` :

1. **`Csp_header_has_no_unsafe_inline_in_style_src`** — l'en-tête servi ne contient plus
   `unsafe-inline` dans `style-src`. Subtilité : plusieurs en-têtes CSP coexistent
   (.NET ajoute `frame-ancestors 'self'` sur les réponses Blazor, docs/07) ; ils se
   combinent par **intersection**, le test les fusionne donc avant de vérifier.
2. **`No_csp_violation_on_main_pages`** — en Chromium réel, navigation sur 6 pages
   (accueil, catalogue, fiche, panier, checkout, arabe RTL) en écoutant la console :
   toute ressource bloquée par la CSP y laisserait un « Refused to… ». Zéro toléré.

## Autopsie n° 1 : les clés de traduction brutes en conteneur

**Symptôme.** En faisant tourner la pile en conteneurs (Production), les tests E2E
Admin et Account échouaient : après un clic sur `/admin/products`, la page affichait
`AdminProductsTitle` au lieu de « Produits ».

**Diagnostic.** Le middleware de locale (`LocalizationSetup`) ne posait la culture que
si l'URL porte le préfixe `/fr|en|ar`. Or le layout génère des liens **absolus sans
préfixe** (`/cart`, `/admin/products`, `/catalog`) : ces requêtes gardaient la culture
du **système hôte** — fr-FR sur un poste Windows français (le bug était invisible en
dev, et les E2E passaient), culture **invariante** dans un conteneur Linux → le
`ResourceManager` ne trouvait aucune ressource et rendait les clés brutes. Le circuit
SignalR (`/_blazor` sans préfixe) était dans le même cas.

**Correctif** (`LocalizationSetup.cs`) : le cookie `sp_locale` — décrit depuis le module 8
mais jamais implémenté — est posé à chaque requête préfixée ; toute URL sans préfixe
reçoit la culture du cookie (sinon `Accept-Language`, sinon `fr`), **sans redirection**
(un 302 casserait `/signin-oidc` et les assets ; seule la racine `/` redirige, comme
avant). Le circuit SignalR hérite ainsi de la bonne culture.

> **Piège** : un test qui passe sur la machine du développeur peut dépendre de la
> **culture de l'OS**. Les E2E ne l'ont révélé qu'en conteneur — la culture par défaut
> d'un conteneur .NET est invariante, pas celle du poste.

## Autopsie n° 2 : le bouton « Suivant » désactivé pour toujours (course latente)

**Symptôme.** Après le correctif de locale, le test Checkout échouait : sur `/checkout`,
le bandeau affichait « 🛒 1 » mais l'assistant « 0 article(s) » — bouton « Suivant »
désactivé indéfiniment (Playwright : « waiting for element to be… enabled », 57 essais).

**Diagnostic.** Le panier se restaure **en asynchrone** depuis le session storage
(`CartSync`, via interop JS après le premier rendu) : le badge, qui observe le
`CartStore`, se re-rend à la restauration ; l'assistant de commande, lui, n'observait
que la machine à états (`Watch(Machine)`) et lisait `Cart.Lines.Count` une seule fois —
un rendu initial à 0 ligne restait figé. Course **latente** depuis toujours : le décalage
de timing introduit par le correctif de locale (ressources satellite chargées pour la
culture fr) l'a rendue systématique en conteneur.

**Correctif** (`Checkout.razor`) : l'assistant observe aussi le panier — le `CartStore`
est un `Store<T>` donc un `IObservableState`, le pattern `Watch` existant suffit :

```csharp
protected override void OnInitialized()
{
       Watch(Machine);
       if (Cart is IObservableState cartState) Watch(cartState);
}
```

> **Piège** : tout état restauré en asynchrone (storage, réseau) doit être **observé**
> par les composants qui le lisent — un rendu « une seule fois » est une course en
> attente. La règle d'or n° 4 s'applique : ce n'est pas le correctif de locale qui a
> créé le bug, c'est lui qui l'a révélé.

## Autopsie n° 3 : `listen()` gelé dans le noyau Windows (incident d'infrastructure)

Pendant cette évolution, tout lancement local de `ShopPulse.Web` se figeait au démarrage.
`dotnet-stack` a montré le thread principal bloqué dans l'appel natif **`Winsock.listen()`**
— un appel noyau qui ne rend jamais la main. Conséquences : processus zombies
in-exterminables sans droits admin (`taskkill` : « aucune instance », `Stop-Process -Force`
sans effet), ports 5026/5299/5027/5030+ définitivement bloqués (`WSAEADDRINUSE` sur des
sockets invisibles dans `netstat`). L'API, elle, démarrait normalement ; Python bindait
les mêmes ports sans problème. Signature d'un pilote de sécurité (antivirus/EDR)
interceptant `listen()`.

**Contournement** : vérifications menées contre la pile en conteneurs — api et web
publiés sur 5301/5040 via `docker/compose.ports-override.yml` (surcharge temporaire),
cibles des tests configurées par `SHOPPULSE_WEBURL` / `SHOPPULSE_APIURL` (le test A11y
et l'utilitaire Screenshots, qui codaient 5026 en dur, ont été rendus configurables).
Redémarrage de Windows nécessaire pour libérer les ports fantômes.

**Correctif annexe** : le build Docker échouait (`NETSDK1152`, fichiers
`staticwebassets` en double) parce que `.dockerignore` n'excluait pas les `bin-local/` —
ajouté.

## Vérifications effectuées

- build Release 0 avertissement / 0 erreur ; architecture 14/14 ;
- en-tête servi : `style-src 'self'` (vérifié par `curl` et par le test E2E) ;
- E2E **7/7** en navigateur réel contre la pile conteneurisée sous CSP durcie —
  tunnel d'achat complet, compte client, back-office, refresh token, captures
  d'écran, et les 2 tests CSP ;
- axe **5/5** (5 pages × 3 langues, RTL inclus) : aucun rendu cassé par le retrait
  d'`unsafe-inline` ;
- non-régression i18n : `/admin/products` sans préfixe affiche « Produits » (sonde
  Playwright), le parcours admin complet repasse.

**Robustesse des tests, au passage** : le test Admin créait sa commande sur le produit
n° 10 en dur — après des dizaines de runs, son stock était épuisé (409 « Stock
insuffisant »), un échec sans rapport avec le code testé. Le test choisit désormais
dynamiquement un produit **en stock** via l'API. Même piège documenté que les E2E à
cibles configurables : un test ne doit dépendre ni de l'OS, ni d'un état de données
que les autres tests consomment.

## Bilan

- `style-src 'self'` est plus fort que des hashes : il n'y a plus rien à autoriser,
  et le test E2E garantit que ça le reste ;
- une CSP se vérifie **en navigateur réel** : les violations sont silencieuses côté
  serveur, visibles uniquement en console ;
- la culture d'une requête ne doit **jamais** dépendre de l'OS qui héberge : un
  middleware de locale doit tout couvrir, y compris les URL sans préfixe et le circuit ;
- les E2E à cibles configurables (`SHOPPULSE_WEBURL`/`SHOPPULSE_APIURL`) ont permis de
  valider la pile conteneurisée sans toucher aux tests — la règle « tout appel a une
  cible configurable » vaut aussi pour les suites de tests.
