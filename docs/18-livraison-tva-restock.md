# 18 — Livraison, TVA et restock

> **Contexte.** Le panier promettait « Livraison : calculée à l'étape suivante » — sans
> jamais la calculer. Cette évolution tient la promesse : frais de port réels avec
> seuil de gratuité, TVA en prix TTC (B2C), et restock automatique à l'annulation.
> Le tout sans jamais faire confiance au navigateur.

## Une seule règle, deux consommateurs : `StoreConfig`

```csharp
public sealed record StoreConfig(decimal ShippingFlatRate, decimal ShippingFreeAbove,
                                 decimal VatRate, string Currency)
{
    public decimal ShippingFor(decimal subtotal) => subtotal >= ShippingFreeAbove ? 0m : ShippingFlatRate;
    public decimal VatIncludedIn(decimal ttc) => Math.Round(ttc * VatRate / (1 + VatRate), 2);
}
```

- **Côté API** : construit depuis la configuration (`Store:Shipping:FlatRate` 5,90 €,
  `Store:Shipping:FreeAbove` 60 €, `Store:VatRate` 0.20 — surchargeables par
  environnement) et appliqué à la création de la commande ;
- **Côté Web** : récupéré via **`GET /api/store/config`** (cache 5 min) pour afficher
  dans le panier exactement ce que le serveur facturera.

Le panier montre désormais le port réel, l'indice « Plus que X € pour la livraison
offerte ! », le total complet et la ligne « dont TVA 20 % ».

## La commande porte des instantanés commerciaux

`Order` gagne `ShippingCost` et `TaxRate` (migration `AddShippingAndTax`, défauts
rétrocompatibles : port 0 pour l'existant) :

- `Subtotal` = marchandises TTC ; `Total` = sous-total + port ;
- `TaxAmount` = TVA **incluse** `(Total × taux / (1 + taux))` — les prix B2C sont TTC,
  la TVA se déduit, ne s'ajoute pas ;
- le taux est **copié sur la commande** : si la boutique change ses tarifs demain,
  l'historique ne bouge pas (même principe que les prix des lignes, phase 3).

Le paiement (phase 6) encaisse automatiquement le bon montant : Stripe lit `Total`.
L'email de confirmation ventile sous-total / livraison / total / dont TVA.

## Restock à l'annulation

Annuler une commande (transition `Pending|Paid → Cancelled`, phase 5) **rend les
marchandises au stock** : `RestockAsync` — miroir du décrément atomique, un seul
`UPDATE … RETURNING` par ligne, dans la **même transaction** que le changement de
statut. Les nouveaux niveaux repartent en SignalR (temps réel) et invalident le cache
catalogue. Un produit supprimé du catalogue entre-temps est ignoré proprement.

**Limite.** Le remboursement Stripe n'est pas déclenché par l'annulation (évolution :
appeler l'API de refund avant le restock).

## Vérifications effectuées

- Commande 13 € → port **5,90 €**, TVA 3,15 €, total 18,90 € ; commande 123,45 €
  → port **offert** (seuil 60 €), TVA 20,58 € ✓ ;
- annulation admin → stock produit **+1** relu, email « Commande annulée » ✓ ;
- `GET /api/store/config` → `{5.90, 60, 0.20, EUR}` ✓ ;
- build 0/0 ; architecture **14/14** ; **E2E 4/4** — les tests E2E acceptent désormais
  `SHOPPULSE_APIURL` / `SHOPPULSE_WEBURL` (cibles configurables).

## Note d'environnement (machine de dev)

Cette évolution a été développée et vérifiée sur une machine dont des processus
zombies (réseau kernel bloqué, sans reboot possible) verrouillaient `bin/Debug` **et**
`bin/Release` et squattaient les ports 5299/5298. Solutions durables ajoutées au projet :

- **`DesignTimeDbContextFactory`** (Infrastructure) : `dotnet ef` s'exécute depuis le
  seul projet Infrastructure — bonne pratique en soi pour un DbContext en classlib ;
- build alternatif `BaseOutputPath=bin-local/` (gitignoré) + exécution directe des DLL ;
- tests E2E à cibles configurables (voir ci-dessus) ;
- `scripts/dev.ps1` détecte un port 5299 occupé et bascule automatiquement.

Après un redémarrage de la machine, tout redevient standard — ces outils restent utiles.
