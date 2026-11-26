# 33 — Responsive mobile : mesuré, corrigé, verrouillé par un test

> **Contexte.** Constat du propriétaire : « le site n'est pas adaptatif sur les petits
> téléphones, et les éléments des différentes pages non plus ». Plutôt qu'un audit à
> l'œil, la méthode du projet s'applique : **mesurer** (à 375 px, la largeur d'un
> petit téléphone), corriger ce qui déborde réellement, puis **verrouiller** par un
> test pour que ça ne régresse jamais.

## L'audit : trois débordements réels, pas cinquante

Sonde Playwright à 375×812 sur les pages clés — mesure `scrollWidth − clientWidth`
et liste des éléments coupables :

| Page | Avant | Coupable |
|---|---|---|
| accueil, catalogue, fiche, tunnel | **OK** | (grilles `auto-fill`, media queries déjà en place) |
| panier rempli | +29 px | lignes de panier en `flex` sans retour à la ligne |
| admin commandes | **+284 px** | `<table>` plus large que l'écran |
| admin produits | +259 px | idem |

Le constat du propriétaire était donc juste, et localisé : le socle responsive
existait (en-tête repliable, grilles fluides), mais **les tables et les lignes de
panier n'avaient jamais été éprouvées sur petit écran**.

## Les corrections

- **Tables** : chaque table du back-office est enveloppée dans
  `.table-scroll` — un conteneur `overflow-x: auto` avec `role="region"`,
  `aria-label` et `tabindex="0"` : la table défile horizontalement **dans** son
  conteneur, la page ne bouge plus, et la région défilante est atteignable au
  clavier (avec anneau de focus visible). Le tableau reste un tableau — pas de
  `display: block` qui casserait les sémantiques pour les lecteurs d'écran.
- **Lignes de panier** (sous 720 px) : `flex-wrap: wrap` — l'info produit prend sa
  ligne, quantité/total/« Retirer » s'enroulent dessous ; le récapitulatif passe en
  pleine largeur.
- **Navigation principale** : `flex-wrap: wrap` — « Commande » n'est plus rognée.
- **Tunnel** : pastilles d'étapes en retour à la ligne (l'empilement 2 colonnes → 1
  était déjà livré avec docs/32).

## Le verrou : un test E2E « zéro défilement horizontal »

`MobileResponsiveE2eTests` : viewport 375×812, parcours des pages clés — accueil,
catalogue, fiche, **panier rempli**, tunnel (deux étapes), **admin commandes et
produits** (avec connexion) — et assertion `scrollWidth ≤ clientWidth` à chaque
étape. Toute régression responsive future (une table ajoutée sans conteneur, un
`min-width` imprudent) fera échouer la CI.

## Autopsie express : « le CSS servi ne contient pas mes règles »

Le test mobile échouait encore après correction : le conteneur servait **l'ancien
`app.css`** — le `--build` incrémental de Docker avait réutilisé une couche périmée
(le fichier publié dans l'image ne contenait pas les nouvelles règles, vérifié par
`grep` DANS le conteneur). Rebuild `--no-cache` → règles présentes → tests verts.
Réflexe à retenir : quand le comportement ne reflète pas le code, vérifier **ce qui
est réellement servi** (curl + grep dans le conteneur) avant de douter du code.

> **Limite environnement** : en fin de session, le poste de développement (conteneurs
> sous pression après des heures de rebuilds, zombies Windows de docs/24) produit des
> timeouts E2E erratiques — un test différent à chaque run, tous verts en CI. La CI
> reste l'arbitre ; les attentes post-login ont été majorées à 30 s (redirections +
> échange de jeton + connexion du circuit sur machine chargée).

## Vérifications effectuées

- sonde 375 px : les 3 débordements corrigés, zéro régression sur les pages saines ;
- E2E **11/11** (nouveau `MobileResponsiveE2eTests` inclus) ; axe **6/6** ;
- captures mobiles avant/après : panier lisible en deux rangées, tables défilantes.

## Bilan

- le responsive se traite comme un bug : **reproduire (mesurer), corriger,
  verrouiller par un test** — pas comme une opinion ;
- une table large n'est pas un problème, une table qui fait défiler la PAGE en est
  un : le conteneur défilant accessible est le bon compromis (données tabulaires
  préservées, page stable) ;
- la chaîne de livraison a des étages : code → build → image → conteneur — le
  diagnostic « ça ne marche pas » commence par identifier **quel étage** sert quoi.

**Limites** (volontaires) : pas de menu « hamburger » (la nav s'enroule, suffisant à
ce volume) ; pas de variantes intermédiaires tablette (le point de rupture unique
720/900 px couvre l'essentiel) ; les pages compte/profil suivront le même traitement
si un débordement y apparaît — le test les couvrira en les ajoutant à sa liste.
