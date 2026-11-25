# 29 — Codes promo : une remise qui ne ment jamais

> **Contexte.** Deuxième feature boutique du workflow d'équipe. Le code promo est un
> classique e-commerce — et un piège classique : la remise affichée au panier doit
> être **exactement** celle facturée, le code ne doit jamais être consommé deux fois,
> et rien de tout cela ne doit faire confiance au navigateur.

## Le modèle : deux types, trois garde-fous en base

`promo_codes` (migration `AddPromoCodes`) : `Code` (unique, **stocké en majuscules** —
la casse est normalisée à l'écriture comme à la lecture), `Kind` (`Percent` ou
`Amount`, en clair dans la base), `Value`, `ExpiresAt?`, `MaxUses?`, `UsedCount`,
`Active`. Garde-fous schéma : unicité du code, `CHECK` percent ∈ ]0, 100], précisions
monétaires. Deux codes de démonstration sont semés (`BIENVENUE10` −10 %, `FIXE5` −5 €).

La formule de remise est **une seule fonction Domain** (`PromoDiscount.Compute`),
partagée par les trois endroits qui en ont besoin : l'aperçu panier, l'endpoint de
validation, et la création de commande — jamais de divergence d'arrondi entre
l'affichage et la facturation. Le montant fixe est plafonné au sous-total (jamais
de total négatif).

## La consommation : atomique, transactionnelle, rejouable sans double compte

C'est le cœur du chapitre. La création de commande (`EfOrderService`) :

1. valide le code (`FindUsableAsync` : actif, non expiré, non épuisé) ;
2. **consomme** par UPDATE conditionnel — la base re-vérifie toutes les conditions :

```sql
UPDATE promo_codes SET "UsedCount" = "UsedCount" + 1
WHERE "Code" = @code AND "Active"
  AND ("MaxUses" IS NULL OR "UsedCount" < "MaxUses")
  AND ("ExpiresAt" IS NULL OR "ExpiresAt" > now());
```

3. l'UPDATE est **enrôlé dans la transaction de la commande** : un stock insuffisant
   plus loin, ou la violation d'unicité de la clé d'idempotence, annule aussi la
   consommation (rollback) — jamais de code « brûlé » pour une commande qui n'existe pas ;
4. le **rejeu idempotent** renvoie la commande existante (instantanés `PromoCode` +
   `Discount` persistés sur la commande) **sans re-consommer**.

C'est exactement la technique du décrément de stock (docs/12) : la règle vit dans
la base, la course « deux commandes, une utilisation restante » est impossible.

La commande gagne deux instantanés : `PromoCode` et `Discount`. Le total devient
`marchandises − remise + livraison`, et le seuil de livraison offerte s'apprécie sur
le sous-total **après remise** (règle documentée, appliquée pareil au panier).

## API : aperçu sans consommation, autorité à la commande

- `POST /api/promo-codes/validate` — public : code + sous-total → code normalisé,
  type, valeur, remise. **Ne consomme rien** : c'est l'aperçu du panier. Un code
  peut donc être « validé » dans deux paniers et n'être consommé que par la première
  commande — la seconde recevra 400 si `MaxUses` est atteint (comportement voulu,
  documenté).
- `POST /api/orders` accepte `promoCode` : re-validation + consommation + recalcul
  **serveur** — la remise affichée au panier n'est jamais transmise, seulement le code.
- Administration sous `CanManageStock` : `GET/POST /admin/promo-codes`,
  `PUT /admin/promo-codes/{id}/active`.

## Web

- **Panier** : champ code + « Appliquer » (validation serveur), code appliqué affiché
  avec bouton « Retirer », ligne « Remise » dans le récapitulatif, seuil de livraison
  offerte recalculé après remise. Le code vit dans `CartState` (`CartStore.SetPromoCode`)
  et voyage au tunnel via `ICartGateway.PromoCode` — le panier reste la seule source
  de vérité.
- **Checkout** : transmet `Cart.PromoCode` à la création ; le total affiché à
  l'étape paiement vient de la commande persistée (donc de la remise serveur).
- **Back-office** : page `/admin/promo-codes` (création — code, type, valeur,
  utilisations max —, liste avec compteur, activation/désactivation), 4e entrée de
  navigation, 19 clés i18n × 3.

> **Limite** : le code promo n'est **pas persisté** dans le session storage du panier
> (les lignes le sont, pas le code — le format de stockage existant n'a pas été
> modifié) : un rechargement complet de page fait perdre le code appliqué, à
> ressaisir. L'aperçu panier ne se re-valide pas non plus tout seul si le code expire
> pendant la navigation — la commande, elle, refuse proprement.

## Tests

- **Intégration** (`PromoTests`, 18/18 au total) : validation (pourcentage, montant
  plafonné, casse libre, inconnu → 400), commande avec code (remise exacte, total
  cohérent, compteur incrémenté), code invalide → 400 à la commande, création admin
  (normalisation majuscules, doublon → 400), désactivation → le code ne valide plus,
  403 pour un client simple.
- **E2E** (`PromoE2eTests`, 9/9 au total) : ajout au panier, application de
  `BIENVENUE10`, ligne de remise négative, total réduit, code affiché avec « Retirer ».
- **A11y** : 5/5 — le champ promo et la page admin passent axe.

## Vérifications effectuées

- build Release 0/0 ; architecture 14/14 ; intégration **18/18** ; E2E **9/9**
  (deux runs stables) ; axe **5/5** — contre la pile conteneurisée ;
- migration appliquée au démarrage, codes semés vérifiés en base
  (`BIENVENUE10|Percent|10.00`, `FIXE5|Amount|5.00`).

## Bilan

- « le serveur re-calcule tout » n'est pas un slogan : le panier n'envoie que le
  **code**, jamais la remise — l'aperçu et la facturation partagent la même fonction
  Domain, donc le même arrondi ;
- la consommation conditionnelle en transaction est le même pattern que le stock :
  quand une règle doit survivre à la concurrence, elle s'écrit en SQL, pas en C# ;
- un aperçu sans effet de bord (`/validate`) et une autorité unique (la commande) :
  deux endpoints, deux responsabilités, zéro ambiguïté.

**Limites** (volontaires) : pas de cumul de codes, pas de restriction par catégorie
ou par client, pas de dates de début (seulement d'expiration), pas d'historique
d'utilisation par commande (le compteur global suffit ici — la commande porte son
instantané), email de confirmation sans ligne « remise » détaillée (le total est
exact), champ promo non localisé (comme le reste du récapitulatif panier — même
Limite que docs/28 pour le catalogue public).
