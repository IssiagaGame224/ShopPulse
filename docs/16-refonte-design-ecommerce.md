# 16 — Refonte design e-commerce

> **Contexte.** Après le back-office (phase 5), toutes les pages publiques passent au
> style e-commerce de référence (fiche type joutech.ma) : fil d'Ariane, bloc prix/promo,
> caractéristiques, produits liés, accueil vitrine, panier détaillé. La refonte consomme
> les champs enrichis en phase 5 (`Sku`, `OldPrice`, `Features`) — **sans aucune
> migration ni changement d'API**.

## Design system : de nouveaux jetons, pas de valeurs en dur

`tokens.css` gagne les couleurs sémantiques de la boutique, avec leurs variantes sombres :

- `--sp-color-muted` (#595959, 7:1 sur blanc) : textes secondaires, prix barrés ;
- `--sp-color-promo` (#b32121, 7,4:1 avec blanc) : badges et rupture de stock ;
- `--sp-color-success` (#1a7f37, 4,6:1) : disponibilité ;
- `--sp-color-border`, `--sp-shadow`, espacements intermédiaires.

Tous les contrastes respectent WCAG 2.2 AA **dans les deux thèmes** — vérifié par la
suite axe (5 pages, fr/en/ar, 0 violation).

## Les pages refondues

### Fiche produit (`/product/{id}`)

Organisation e-commerce classique : **fil d'Ariane** (`nav[aria-label]` + `ol`,
catégorie cliquable vers le catalogue filtré) → **deux colonnes** (visuel | infos :
référence SKU, bloc prix avec badge « −20 % » et prix barré, description, CTA +
stock temps réel) → **caractéristiques** (liste à puces dans un encadré) →
**produits liés** (même catégorie via `GetPageAsync(…, category)`, hors produit
courant, 4 max). Le JSON-LD s'enrichit (`sku`, `offers` avec prix et disponibilité).
Une page « Produit introuvable » remplace le chargement infini.

Accessibilité des prix : le prix barré est un `<s>` **préfixé d'un texte invisible**
(« Ancien prix : … ») — les lecteurs d'écran n'annoncent pas la rayure.

### Catalogue

Barre de filtres en encadré (recherche + catégorie + bouton primaire), **compteur de
résultats** (« 202 produit(s) — page 1 / 17 »), cartes enrichies. Le formulaire GET
sans circuit et la pagination par URL sont inchangés.

### Accueil (`/`) — fin du « Hello, world! »

Vitrine SSR : **hero** (dégradé primaire, CTA catalogue), **tuiles catégories** (depuis
l'API), section **Promotions** (produits `IsOnSale` en stock) et **Nouveautés** (ids
décroissants — Guid… non : ids identity, donc les créations back-office apparaissent).

### Panier et tunnel

Lignes détaillées (vignette, nom lié, prix unitaire, stepper quantité avec `aria-label`,
total de ligne, retrait), **bloc récapitulatif** (sous-total, livraison, total) et CTA
« Passer commande ». Tunnel : étapes en pastilles numérotées (CSS `counter`),
confirmation en encadré avec lien « Suivre mes commandes ».

**Piège E2E.** Le CTA du panier et le lien du menu pointent tous deux vers
`checkout` : les tests Playwright (mode strict) scindent sur `a[href='checkout']` —
sélecteur précisé en `nav a[href='checkout']`.

### Carte produit (`ProductCard`, design system)

Nouveau paramètre optionnel `OldPrice` : badge « −X % » (pourcentage calculé),
prix promo + `<s>` barré. Rétrocompatible : les pages existantes compilent sans
changement (catalogue virtualisé mis à jour pour en profiter).

## Piège CSS — le `min-height` anti-CLS devenu piège

La grille du catalogue avait `min-height: 900px` (réserver l'espace du streaming,
module 2). Avec des cartes en `flex` et un lien poussé en bas (`margin-block-start:
auto`), les rangées d'une seule ligne (sections d'accueil) s'étiraient à 900 px de
haut. Correction : `align-items: start` et suppression du `min-height` — les
squelettes (320 px × 8) réservent déjà l'espace pendant le chargement.

## Vérifications effectuées

- Build 0/0 ; architecture **14/14** ; **E2E 4/4** (invité, compte, admin + captures) ;
- **axe 5/5 pages** sans violation (catalogue, fiche, panier, en, ar/RTL) ;
- **vérification visuelle par captures Playwright** (accueil, fiche promo, catalogue,
  panier) : organisation conforme à la référence, deux défauts détectés et corrigés
  (grille étirée, récapitulatif panier en ligne).

## Bilan des notions

- le design consomme le modèle, il ne le modifie pas : zéro migration, zéro endpoint ;
- couleurs sémantiques dans les jetons (jamais de hex dans les pages) + variantes sombres ;
- prix barré accessible : `<s>` + texte invisible, badge avec pourcentage calculé ;
- les sélecteurs E2E sont un contrat : tout nouveau lien vers une page existante peut
  casser le mode strict de Playwright ;
- une capture d'écran automatisée vaut mieux qu'un « ça devrait rendre bien ».

**Prochaine étape** : phase 6 — paiement Stripe (Payment Intent côté serveur, webhook
de confirmation, passage `Pending → Paid` automatique).
