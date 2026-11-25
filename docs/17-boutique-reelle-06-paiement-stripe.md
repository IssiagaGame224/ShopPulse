# 17 — Vraie boutique, phase 6 : le paiement Stripe

> **Contexte.** Dernière phase fonctionnelle : le tunnel encaisse réellement. Stripe en
> **mode test** (Payment Intent + Payment Element), **webhook signé** comme seule source
> de vérité, passage automatique `Pending → Paid`. Sans clés configurées, un
> **simulateur** permet de tester toute la boucle en dev.

## L'architecture du paiement

```
Navigateur                API                          Stripe
   │  « Payer »             │                             │
   │── crée commande ──────▶│ Pending (idempotent, ph. 3) │
   │── POST /orders/{id}/payment ─▶ crée PaymentIntent ──▶│
   │◀── clientSecret ───────│  (idempotence = orderId)    │
   │  Stripe.js confirme (Payment Element) ──────────────▶│
   │                        │◀── webhook SIGNÉ ───────────│
   │                        │  Pending → Paid + email     │
   │── sonde GET /orders/{id} jusqu'à Paid ─▶             │
```

Trois principes non négociables :

1. **Le montant vient de la commande persistée** (recalculée serveur, phase 3) —
   convertie en centimes, devise de la commande ;
2. **Le retour navigateur n'est jamais une preuve** : `confirmPayment` côté JS ne fait
   qu'initier ; seul le **webhook signé** (`EventUtility.ConstructEvent` valide la
   signature, 400 sinon) déclenche `Pending → Paid` via la machine de transitions
   (phase 5) — idempotente, donc les rejeux de webhook sont sans effet (pas de second
   email) ;
3. **Idempotence à tous les étages** : commande (clé client, phase 3), Payment Intent
   (clé Stripe = id de commande : recréer une session rend le MÊME intent), transition
   (rejouer « Paid » est un no-op).

## Le gateway interchangeable

`IPaymentGateway` (Application) a deux implémentations, choisies à la configuration :

- **`StripePaymentGateway`** (Stripe.net 49.2.0) : actif si `Stripe:SecretKey` existe
  (user-secrets en dev, variables d'environnement en conteneur — jamais dans le dépôt) ;
- **`FakePaymentGateway`** : sinon. Sessions déterministes (`fake_<orderId>`), et un
  endpoint `/orders/{id}/payment/fake-confirm` — **mappé uniquement dans ce mode** —
  rejoue le rôle du webhook. Toute la boucle reste testable sans compte Stripe,
  y compris les E2E.

Pour brancher de vraies clés de test :

```bash
cd src/ShopPulse.Api
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."   # via `stripe listen --forward-to`
```

## Le tunnel, côté navigateur

« Payer » ne confirme plus immédiatement : il crée la commande puis ouvre la session.

- **Mode Stripe** : `stripe-payment.js` (chargement paresseux de `js.stripe.com/v3/`,
  module JS importé par interop) monte le **Payment Element** dans `OnAfterRenderAsync`
  (jamais pendant le prerender — règle du cours) ; « Confirmer le paiement » appelle
  `stripe.confirmPayment` (`redirect: 'if_required'`), puis le composant **sonde** la
  commande (~15 s) jusqu'à ce que le webhook l'ait passée à `Paid` ;
- **Mode simulateur** : encadré « Mode démo » + bouton « Simuler le paiement ».

La clé d'idempotence de la commande reste stable pendant toute la tentative : un retry
du clic « Payer » rejoue la même commande, une nouvelle session rend le même intent.

## Sécurité

- `POST /orders/{id}/payment` : même règle d'accès que la lecture (commande liée → son
  compte ; invitée → id non devinable) ; **409** si la commande n'attend plus de paiement ;
- webhook : signature validée avant tout traitement, événements non liés → 200 ignoré ;
- CSP (Report-Only) ouverte à `js.stripe.com` (script + iframes 3DS) et
  `api.stripe.com` (connect) — **Limite** : en CSP stricte, le script injecté
  dynamiquement devra porter le nonce ;
- `Order.PaymentIntentId` persisté (migration `AddPaymentIntentId`) pour la
  traçabilité et le support.

## Vérifications effectuées

- Build 0/0 ; architecture **14/14** ; axe **5/5** ; **E2E 4/4** ;
- boucle API complète en mode simulateur : session créée → **rejouée à l'identique**,
  fake-confirm → `Paid`, rejeu idempotent (200, **pas de second email** — Mailpit :
  exactement 2 messages, confirmation + statut), nouvelle session sur commande payée
  → **409**, commande inconnue → **404**, webhook sans rapport → 200 accusé ;
- E2E invité et compte adaptés : clic « Payer » → « Simuler le paiement (démo) » →
  « Commande confirmée », avec assertions stock (−1) et emails (≥ 2).

**Non vérifié** (faute de clés) : le chemin Stripe réel (création d'intent, Payment
Element, webhook signé). Le code est écrit contre l'API Stripe.net standard ; le
premier run avec de vraies clés de test reste à faire (`stripe listen` pour le webhook).

## Bilan des notions

- le webhook signé est la seule preuve de paiement ; le navigateur n'initie que ;
- idempotence de bout en bout : commande, intent (clé = orderId), transition ;
- un gateway interchangeable rend la boucle testable sans dépendre d'un tiers ;
- monter un élément JS après le rendu, jamais pendant le prerender ;
- le montant se calcule là où la donnée est fiable : la base.

**La feuille de route « vraie boutique » est terminée** (6 phases + refonte design).
Évolutions possibles : remboursements Stripe (Cancel → refund), refresh token OIDC,
emails localisés, pattern Outbox, CSP stricte, i18n des pages compte/admin.
