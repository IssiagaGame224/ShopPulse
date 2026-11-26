# 32 — Tunnel de commande « boutique pro » : étapes numérotées et récapitulatif latéral

> **Contexte.** Demande du propriétaire, sur la référence [joutech.ma](https://joutech.ma/)
> (capture de leur page de commande) : un tunnel qui ressemble à une vraie boutique —
> étapes numérotées à gauche, carte « Votre commande » collante à droite avec
> miniatures, remise, livraison et total toujours visibles.

## La mise en page

```
┌─ ① Panier ─ ② Livraison ─ ③ Paiement ─┐   ← pastilles numérotées (étape courante en primaire)
┌──────────────────────────┐  ┌────────────────────┐
│ ① Informations de        │  │ Votre commande     │   ← sticky
│   livraison              │  │ [img] Nom ×1  14,50│
│   [Nom]      [E-mail]    │  │ Sous-total   14,50 │
│   [Adresse            ]  │  │ Remise       −1,45 │
│   [Code postal]          │  │ Livraison    5,90  │
│   [Continuer]            │  │ Total        20,40 │
└──────────────────────────┘  └────────────────────┘
```

- `checkout-layout` : grille `1fr 22rem` ; sous 900 px, une colonne et le
  récapitulatif **remonte en premier** (`order: -1`) — le client voit ce qu'il paie
  avant de remplir (le responsive global est le chantier suivant, docs/33).
- Sections numérotées (« ① Informations de livraison », « ② Paiement ») dans des
  cartes ; formulaire en grille deux colonnes (champs larges sur toute la ligne),
  labels au-dessus, focus visible, messages de validation en rouge.
- **Tous les sélecteurs E2E existants préservés** (`#fn`, « Continuer », « Payer »,
  « Simuler le paiement », `text=article(s)`…) : le redesign ne casse aucun test.

## Le récapitulatif : aperçu avant, autorité serveur après

Une seule carte, deux sources selon l'étape :

- **avant la création de la commande** : aperçu recalculé avec les MÊMES règles que
  le serveur — prix du catalogue (`IProductService`), livraison/TVA via
  `StoreConfig` (la règle unique, docs/18), remise via `PromoDiscount` (docs/29) sur
  le code du panier (`ICartGateway.PromoCode`) ;
- **dès que la commande existe** (étapes paiement/confirmation) : les valeurs
  **persistées** (`Order.Subtotal/Discount/ShippingCost/Total/TaxAmount`) — ce que
  le client voit est ce que le serveur a calculé, au centime.

Même discipline que le panier (docs/29) : l'aperçu n'envoie jamais rien au serveur,
il ne fait que préparer l'œil.

> **Piège Razor** : `@((x) * 100).ToString("0")` affiche… `20,00.ToString("0")` !
> L'expression `@(...)` s'arrête à sa parenthèse fermante ; ce qui suit est du texte
> littéral. Le `.ToString()` doit être DANS l'expression :
> `@(((x) * 100).ToString("0"))`. Attrappé par la vérification visuelle (capture
> d'écran), pas par les tests — les deux se complètent.

## Vérifications effectuées

- build Release 0/0 ; E2E **10/10** (deux runs stables — le tunnel complet passe :
  panier → livraison → paiement → confirmation) ; axe **6/6** ;
- vérification visuelle par capture : pastilles d'étapes, grille du formulaire,
  carte récapitulative avec miniature, sticky — conforme à la référence.

## Bilan

- un redesign de tunnel = **zéro changement de comportement** : la machine à états,
  l'idempotence et les prix serveur sont intacts ; seuls le balisage et le CSS bougent ;
- garder les sélecteurs E2E stables pendant un redesign, c'est choisir des ancres
  sémantiques (ids, libellés) plutôt que des classes de style ;
- l'aperçu local est un confort, jamais une source : la carte affiche les valeurs
  serveur dès qu'elles existent.

**Limites** (volontaires) : le récapitulatif n'est pas repliable sur mobile (simple
empilement pour l'instant — le chantier responsive global suit) ; pas de choix de
mode de livraison (retrait magasin vs domicile comme joutech — nécessiterait un
champ commande) ; les libellés du tunnel restent en français (même Limite que
docs/28-31 pour les pages publiques).
