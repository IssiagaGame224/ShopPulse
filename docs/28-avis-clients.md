# 28 — Avis clients : une feature full-stack de bout en bout

> **Contexte.** Première fonctionnalité « boutique » ajoutée depuis l'adoption du
> workflow d'équipe (branche `feature/avis-clients`, PR, CI obligatoire). Le périmètre
> touche toutes les couches : migration EF, endpoints, composants, i18n du back-office,
> tests d'intégration et E2E — c'est la démonstration que l'architecture modulaire
> tient ses promesses : chaque couche a un endroit évident où vivre.

## Le modèle : la règle « un avis par client et par produit » dans la base

`ReviewEntity` (table `reviews`) : `ProductId` (FK, `Restrict` comme le catalogue),
`UserId` (sub Keycloak), `AuthorName` (nom d'affichage capturé à la soumission),
`Rating` 1–5, `Comment` (1000 caractères), `CreatedAt`. Trois garde-fous **dans la
base**, pas seulement dans le code :

- index **unique** `(ProductId, UserId)` — un client ne peut pas empiler les avis ;
- contrainte `CHECK ("Rating" BETWEEN 1 AND 5)` ;
- longueurs maximales (64/100/1000) — le schéma double la validation applicative.

La soumission est un **upsert** : un second envoi remplace l'avis existant (note,
commentaire, nom d'affichage), la date de création est conservée.

## API : l'auteur vient du jeton, jamais du corps

- `GET /api/products/{id}/reviews` — public : agrégat (`ProductRating`, moyenne
  arrondie à 0,1) + 50 avis récents ; 404 si le produit n'existe pas.
- `POST /api/products/{id}/reviews` — `RequireAuthorization` : `sub` et
  `preferred_username` lus **dans le jeton** ; validation 400 (note hors bornes,
  commentaire vide) ; invalidation du tag de cache `"catalog"` (la moyenne affichée
  au catalogue change).
- `GET /api/admin/reviews` + `DELETE /api/admin/reviews/{id}` — modération sous
  `CanManageStock`, comme le reste du back-office.

Le record `Product` gagne `AverageRating`/`ReviewCount` (paramètres optionnels —
aucun code existant cassé), calculés en **une requête GROUP BY pour toute la page**
(`RatingsAsync`) : jamais une requête par produit (N+1).

## Web : un îlot interactif dans une page statique

`ProductReviewsSection` (îlot `InteractiveServer` de la fiche produit) :

- la liste et la moyenne se chargent **au prerender** — elles sont dans le HTML
  statique (SEO, premier affichage) ;
- le formulaire (radios 1–5 + `textarea`, `maxlength` aligné sur la base) ne
  s'affiche que pour un utilisateur authentifié (`AuthenticationStateProvider`) ;
  sinon : lien de connexion ;
- l'auteur de son propre avis voit « (votre avis) » et le formulaire pré-rempli
  (modifier = resoumettre) ;
- succès annoncé aux lecteurs d'écran via `ILiveAnnouncer` (polite) ;
- étoiles en texte (`★★★★☆`) avec `aria-hidden` + libellé visible ou
  `visually-hidden` — jamais d'information portée par la seule décoration.

`ProductCard` gagne deux paramètres optionnels (`AverageRating`, `ReviewCount`) :
les étoiles apparaissent sur les cartes du catalogue, de l'accueil et des produits
liés dès qu'un produit a des avis.

Back-office : page `/admin/reviews` (liste + suppression), troisième entrée de la
navigation admin, clés i18n ×3 (`AdminReviewsTitle`, `ColProduct`… — 7 nouvelles).

> **Piège** : le composant ne peut pas s'appeler `ProductReviews` — le record Domain
> du même nom entre en collision dans le code généré par Razor (CS1061 en cascade,
> « 'IReviewService' ne contient pas de définition pour 'FirstOrDefault' »).
> D'où `ProductReviewsSection`.

> **Limite** : la fiche produit (et donc la section avis) est en français en dur —
> c'est l'état existant de la page, pas une régression de cette feature ;
> l'internationalisation du catalogue public reste une évolution. Le back-office,
> lui, est trilingue comme avant.

## Tests

- **Intégration** (`ReviewTests`, 17/17 au total) : le cycle complet en un test —
  soumission (auteur = `client` du jeton), lecture publique + agrégat, agrégat
  remonté sur `GET /products/{id}`, upsert (compteur inchangé, note remplacée),
  400 ×2, 404 produit inconnu, 401 anonyme, 403 client sur la modération,
  liste + suppression admin.
- **E2E** (`ReviewE2eTests`, 8/8 au total) : connexion Keycloak, fiche produit,
  note 4 + commentaire horodaté, avis visible signé « client » avec « (votre avis) »,
  moyenne affichée.
- **A11y** : la fiche produit fait partie des 5 pages auditées — le nouveau markup
  (radios, fieldset, contrastes des étoiles) passe axe 5/5.

## Autopsie : la suite E2E devenue flaky en ajoutant un 4e navigateur parallèle

**Symptôme.** Avec `ReviewE2eTests`, des échecs en rafale et changeants : badge
panier jamais à 1 (Account, Checkout), avis jamais affiché (Review).

**Diagnostic.** Deux causes distinctes :
1. xUnit exécute les **classes** de test en parallèle : 4 Chromium + leurs circuits
   SignalR se disputaient les ressources du poste — les circuits mettaient trop de
   temps à se connecter, les clics « pré-circuit » tombaient dans le vide (le HTML
   prerendu est visible avant l'interactivité — un formulaire soumis trop tôt fait
   même une soumission native qui recharge la page).
2. Mailpit s'était arrêté entre-temps (connexion refusée sur 8025) — un échec
   d'infrastructure, pas de code.

**Correctifs.** Collection xUnit unique `[Collection("e2e")]` : les classes
s'exécutent **séquentiellement** (la suite complète reste sous la minute) ;
soumission d'avis en boucle avec ressaisie (même stratégie que le badge panier —
le projet l'appliquait déjà, elle s'applique à tout formulaire d'îlot).

> **Piège** : un test E2E qui clique dans un îlot interactif doit toujours tolérer
> que le circuit ne soit pas encore connecté — le HTML prerendu ment sur
> l'interactivité. La stratégie « agir → vérifier l'effet → recommencer » est la
> seule robuste.

## Vérifications effectuées

- build Release 0/0 ; architecture 14/14 ; intégration **17/17** ; E2E **8/8**
  (deux runs stables) ; axe **5/5** — le tout contre la pile conteneurisée ;
- migration `AddReviews` appliquée au démarrage de l'API (table + contraintes
  vérifiées par `\d reviews`) ;
- sonde manuelle : page produit anonyme → résumé « Aucun avis », pas de
  formulaire, panier fonctionnel.

## Bilan

- une feature full-stack = un seul fil : entité → migration → endpoints → port →
  client → composant → tests, chaque couche à sa place (les tests d'architecture
  le garantissent) ;
- les règles métier critiques (unicité, bornes) vivent **aussi** dans le schéma —
  le code applicatif n'est pas la seule ligne de défense ;
- l'agrégat se calcule en une requête pour toute une page : le N+1 se prévient au
  moment de l'écriture de la requête, pas au profiling.

**Limites** (volontaires) : pas de pagination des avis au-delà de 50 (un produit
populaire en demanderait) ; pas de réponse du vendeur ni de signalement ; la
modération supprime sans historique (un statut « masqué » serait plus doux) ;
l'auteur est un simple `preferred_username` (pas de lien vers le compte) ; la
fiche produit reste française (voir Limite ci-dessus).
