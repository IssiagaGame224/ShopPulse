# 31 — Accueil « marketplace » : carrousel de gammes animé + CTA WhatsApp

> **Contexte.** Demande du propriétaire, inspirée de la page d'accueil de
> [joutech.ma](https://joutech.ma/) : un **slider animé** présentant les nouvelles
> gammes, et un **bouton WhatsApp flottant** pour le contact direct (très répandu
> sur les boutiques marocaines). Deux ajouts simples en apparence — et deux beaux
> terrains de jeu accessibilité.

## Le carrousel : animé, mais l'utilisateur garde la main

`HeroSlider` (îlot `InteractiveServer` de l'accueil) — le prerender affiche la
première diapositive (le HTML statique suffit), la rotation démarre à la connexion
du circuit. Diapositives construites depuis les **vraies données** : une par gamme
(image d'un produit réel de la catégorie → `/catalog?category=…`), précédée d'une
diapositive « Promotions » avec le pourcentage max réel (→ `/catalog?sort=promo`).

Le pattern WAI « carousel », appliqué :

- **WCAG 2.2.2** (tout contenu animé doit pouvoir s'arrêter) : bouton pause/reprendre
  visible avec `aria-pressed` — pas un nice-to-have, une exigence AA ;
- **pause douce** au survol et au focus clavier (on ne fait pas défiler sous les yeux
  de quelqu'un qui lit ou tabule) ;
- **`prefers-reduced-motion`** : ni rotation ni transition — lu via un petit module
  JS importé dynamiquement (`js/motion.js`), après le rendu ;
- diapositives inactives en `visibility: hidden` : hors tabulation **et** hors lecteur
  d'écran, avec `aria-roledescription` (« carousel », « diapositive ») et
  « Diapositive N sur M » ;
- flèches précédent/suivant + points indicateurs (`role="tab"`, `aria-selected`),
  rotation en boucle dans les deux sens.

> **Piège** : la première version lisait la préférence de mouvement via
> `JS.InvokeAsync("eval", …)` — **bloqué par notre propre CSP** (`script-src` sans
> `'unsafe-eval'`, docs/24). La CSP stricte fait son travail : passage par un module
> JS importé (`import "./js/motion.js"`), comme `stripe-payment.js`.

> **Piège** : le carrousel a remplacé le bandeau hero… et son `<h1>` avec. Le test
> axe de l'accueil (ajouté à cette occasion) l'a immédiatement vu : une page sans
> `h1` n'a pas de nom. Correction : `h1` visuellement masqué qui nomme la page, les
> titres de diapositives restent en `h2`.

> **Piège** : les points indicateurs faisaient ~13 px — violation axe
> **`target-size`** (WCAG 2.2 : cible tactile ≥ 24 px). Le point visuel reste petit
> (`::after` centré), la zone cliquable fait 24 px.

## Le bouton WhatsApp : un lien, pas un script tiers

`MainLayout` (toutes les pages) : pastille verte fixe en bas « fin de ligne »
(`inset-inline-end` — côté gauche en arabe RTL), icône SVG inline, ouverture dans
un nouvel onglet (`target="_blank" rel="noopener"`), `aria-label` explicite.

Décision délibérée : **un simple lien `https://wa.me/<numéro>?text=…`**, pas le
widget script officiel de WhatsApp — zéro JavaScript tiers, zéro iframe, la CSP
stricte reste intacte, et la page ne dépend d'aucun service externe pour s'afficher.

Le numéro vit dans la **configuration** (`Store:WhatsApp` dans `appsettings.json`),
pas dans le composant — le même déploiement peut changer de numéro sans rebuild.

## Vérifications effectuées

- build Release 0/0 ; E2E **10/10** (nouveau `HomeE2eTests` : pause d'abord — elle
  prouve l'interactivité ET fige la rotation pour rendre la suite déterministe —,
  flèche → 2e diapositive, une seule visible, lien wa.me avec le bon numéro,
  `_blank`/`noopener`) ; axe **6/6** (l'accueil `/fr` rejoint la matrice) ;
- vérification visuelle par capture : slide « Promotions en cours » (−20 % réel),
  contrôles, pastille WhatsApp.

## Bilan

- un carrousel accessible coûte trois choses : un bouton pause, le respect de
  `prefers-reduced-motion`, des cibles tactiles de 24 px — tout le reste est du CSS ;
- la CSP stricte transforme les raccourcis (`eval`) en impasses : les modules JS
  importés sont le chemin normal ;
- tester l'accueil à l'axe a payé immédiatement (h1, target-size) : **chaque page
  nouvelle ou refaite rejoint la matrice d'audit** — c'est maintenant la règle.

**Limites** (volontaires) : pas de geste « swipe » tactile sur le carrousel (les
flèches et points suffisent, un swipe est une évolution) ; le texte du message
WhatsApp pré-rempli est en français (la page d'accueil publique reste française —
même Limite que docs/28-29) ; les diapositives sont générées depuis le catalogue,
pas éditables au back-office (un vrai CMS de bannières serait une évolution).
