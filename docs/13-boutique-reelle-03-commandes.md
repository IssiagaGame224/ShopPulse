# 13 — Vraie boutique, phase 3 : les commandes

> **Contexte.** Phase 3 de la transformation en vraie boutique. Le tunnel de commande
> (machine à états du module 3) crée désormais de **vraies commandes persistées** :
> prix recalculés côté serveur, stock décrémenté dans la même transaction, idempotence
> par clé, email de confirmation. Le paiement réel (Stripe) viendra en phase 6.

## Le modèle : des instantanés, pas des références

```csharp
public record OrderLine(int ProductId, string ProductName, decimal UnitPrice, int Quantity);
public record Order(Guid Id, string Email, ..., string Currency, OrderStatus Status,
                    DateTimeOffset CreatedAt, IReadOnlyList<OrderLine> Lines);
```

Principes :

- **Instantané** : nom et prix unitaire sont *copiés* dans la ligne au moment de l'achat.
  Une commande historique ne change jamais, même si le catalogue évolue. `ProductId`
  reste une **référence souple** (pas de clé étrangère vers `products`) : le catalogue
  peut supprimer un produit sans toucher l'historique.
- **Argent** : `decimal(18,2)` + code devise ISO (`Currency`, config `Store:Currency`).
- **Id** : `Guid.CreateVersion7()` — non devinable, triable chronologiquement.
- **Statut** : enum stocké **en clair** en base (`HasConversion<string>()` → `"Pending"`)
  et sérialisé en clair dans le JSON (`JsonStringEnumConverter`).
- `UserId` (sub Keycloak) existe mais reste nul : le lien compte ↔ commandes arrive en
  phase 4.

## Prix recalculés côté serveur — jamais ceux du navigateur

`PlaceOrderRequest` ne transporte que `(ProductId, Quantity)` par ligne. Le serveur
(`EfOrderService`) recharge les produits en base et calcule `UnitPrice`, `LineTotal`,
`Total`. Toute manipulation du DOM ou du circuit Blazor est sans effet sur les montants.

## L'idempotence : la clé qui rend le retry inoffensif

Une écriture réseau peut être rejouée (timeout, double-clic, F5) : sans protection, deux
commandes et deux décréments. La solution tient en trois couches :

1. **Client** : le tunnel génère **une clé par tentative d'achat**
   (`Guid.NewGuid().ToString("N")` à la soumission du formulaire de livraison), stable
   pendant toutes les reprises, envoyée dans l'en-tête `Idempotency-Key`.
2. **Service** : avant tout travail, recherche par clé → si trouvée, **rejeu** de la
   commande existante (HTTP 200, pas d'effet de bord).
3. **Base** : index **unique** sur `IdempotencyKey`. Si deux requêtes de même clé
   s'entrelacent, la seconde `SaveChanges` lève une violation d'unicité PostgreSQL
   (`23505`) → la transaction est annulée (décréments inclus) et l'existante est rejouée.

**Testé** : 3 requêtes simultanées de même clé → un 201, deux 200, **une seule ligne**
en base, un seul décrément.

## La transaction : tout ou rien

Création = insertion de la commande **+** décrément atomique de chaque ligne
(`UPDATE ... WHERE "Stock" >= @q`, phase 2) **dans la même transaction** :

- une ligne en échec → `return` sans `CommitAsync` → le `Dispose` annule tout,
  décréments déjà faits inclus (**rollback vérifié** : ligne 1 décrémentée puis ligne 2
  en rupture → stock de la ligne 1 inchangé, aucune commande en base) ;
- Npgsql enrôle automatiquement les commandes ADO.NET de la connexion dans la
  transaction en cours ; on l'assigne aussi explicitement
  (`cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction()`).

Réponses de `POST /api/orders` : **201** (créée) / **200** (rejeu) / **409** (stock
insuffisant, message nommant le produit) / **400** (panier vide, quantité ≤ 0, email
invalide, produit inconnu, adresse incomplète).

## L'email de confirmation : un effet de bord qui ne bloque jamais

- Port `IOrderEmailSender` (Application), implémentation `SmtpOrderEmailSender`
  (**MailKit**) : configuration `Email:Smtp:Host/Port/From`, sans hôte → simple log.
- **Mailpit** dans docker-compose : capture SMTP de dev (UI http://localhost:8025,
  SMTP 1025) — aucun email ne sort réellement.
- L'envoi est tenté **après le commit**, dans un `try/catch` : un SMTP en panne ne fait
  jamais échouer une commande enregistrée.
- Le rejeu idempotent ne renvoie **pas** d'email (pas de doublon dans la boîte du client).

**Limite.** En production, l'envoi direct après commit reste perfectible (crash entre
commit et envoi = email perdu) : le pattern **Outbox** (événement persisté dans la même
transaction, puis relais) est la suite naturelle. Les emails sont en français invariant —
la locale du client n'est pas encore stockée sur la commande.

## Le tunnel branché

`Checkout.razor` utilise la machine à états existante, enrichie :

- étape *Cart* : nombre d'articles, « Suivant » désactivé si panier vide ;
- étape *Shipping* : la soumission du formulaire génère la clé d'idempotence ;
- étape *Payment* : « Payer » appelle `IOrderPlacer` (bouton désactivé pendant l'appel) ;
  c'est ici que Stripe se branchera (phase 6) ;
- *Confirmed* : référence courte (8 premiers caractères du Guid), total, email ;
- *Failed* : message d'erreur (ex. stock insuffisant) + « Réessayer » **avec la même
  clé** — jamais de doublon.

`ICartGateway` (SharedKernel) gagne `Lines` et `Clear()` : le tunnel lit le panier et le
vide après confirmation, toujours sans référencer la feature Cart. Côté Web,
`OrderApiClient` (Infrastructure) implémente `IOrderPlacer` en HTTP — le retry HTTP est
**sûr précisément parce que** l'endpoint est idempotent (règle du cours : ne jamais
réessayer une écriture non idempotente — ici elle l'est).

## Vérifications effectuées

- Build solution : 0 erreur, 0 avertissement ; architecture : **14/14** ;
- API : création 201 (prix et total recalculés serveur, statut `"Pending"`), rejeu 200
  sans re-décrément ni second email, course 3× même clé → 1×201 + 2×200 + 1 ligne,
  rollback vérifié en SQL, 4 validations → 400, `GET /api/orders/{id}` → 200/404 ;
- email visible dans Mailpit (« Confirmation de votre commande 01A20EC9 ») ;
- **nouveau test E2E Playwright** (`tests/ShopPulse.E2E`, ajouté à la solution) : le
  parcours complet dans Chromium réel — ajout au panier, navigation, formulaire, paiement,
  « Commande confirmée » — avec assertion sur le stock (via API) et l'email (via Mailpit).

**Limite.** `GET /api/orders/{id}` n'a pas encore de contrôle d'accès (le Guid v7 est
non devinable, mais la propriété `UserId` et l'autorisation arrivent en phase 4).

## Bilan des notions

- instantané vs référence : l'historique d'une commande est immuable par construction ;
- prix côté serveur, toujours ; le client n'envoie que des identifiants et quantités ;
- idempotence en trois couches : clé client, rejeu applicatif, unicité en base ;
- transaction = insertion + décréments, tout ou rien ; rollback par simple `Dispose` ;
- email = effet de bord post-commit, jamais bloquant (Outbox en perspective) ;
- une écriture idempotente peut être retentée sans crainte — c'est toute la chaîne qui
  en profite.

**Prochaine phase** : comptes clients (liaison Keycloak `sub` ↔ commandes, pages
« Mon compte », historique).
