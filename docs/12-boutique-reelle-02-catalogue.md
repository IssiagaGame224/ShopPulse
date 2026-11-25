# 12 — Vraie boutique, phase 2 : catalogue complet

> **Contexte.** Phase 2 de la transformation en vraie boutique (voir chapitre 11 pour la
> base PostgreSQL). Objectifs : de **vraies catégories relationnelles**, des **filtres
> exécutés côté serveur** (SQL) et un **stock transactionnel** qui ne peut jamais passer
> sous zéro, même sous concurrence.

## Catégories en base : fini les chaînes libres

Avant : `ProductEntity.Category` était une simple chaîne, et l'UI codait la liste en dur
(`["Audio", "Clavier", "Écran"]` dans `Catalog.razor`). Après :

- **`CategoryEntity`** : table `categories` (Id, Name unique) — les 3 catégories sont
  seedées avec des ids stables (`CatalogSeed.Catégories`) ;
- **`ProductEntity.CategoryId`** : vraie clé étrangère, `DeleteBehavior.Restrict`
  (jamais de suppression en cascade d'un catalogue), index sur la FK ;
- le **Domain ne change pas** : `Product.Category` reste le *nom* (string), obtenu par
  jointure (`Include(p => p.Category)` puis projection). UI, contrats et tests :
  aucun impact.

### La migration `AddCategories` et son backfill

**Piège.** EF Core a scaffoldé 400 instructions `UpdateData` (une par produit seedé) pour
remplir `CategoryId` — fragile (ne couvre que les lignes du seed) et illisible. La
migration a été **réécrite à la main** avec un backfill SQL unique, exécuté **avant** le
drop de l'ancienne colonne :

```sql
UPDATE products SET "CategoryId" = categories."Id"
FROM categories
WHERE categories."Name" = products."Category";
```

Ordre des opérations : créer `categories` → insérer les 3 lignes → ajouter `CategoryId`
→ **backfill SQL** → drop ancienne colonne → index + clé étrangère. La clé étrangère est
ajoutée **après** le backfill, sinon les lignes à `CategoryId = 0` la violeraient.
Le `Down()` est symétrique (re-backfill du texte depuis la jointure).

Vérifié sur la base réelle de la phase 1 : 60 Audio / 80 Clavier / 60 Écran, conforme à
la répartition du seed.

## Filtres et pagination côté serveur

Avant, les deux consommateurs paginaient **en mémoire après avoir tout téléchargé** :
`Catalog.razor` filtrait le catalogue complet, et `ProductApiClient.GetPageAsync`
récupérait les 200 produits pour n'en garder qu'une page.

Après :

- `IProductService.GetPageAsync(skip, take, search, category, ct)` — nouveau paramètre
  `category` ;
- **`GET /api/products/page?skip=&take=&search=&category=`** — `CountAsync` +
  `Skip/Take` en SQL, `take` borné à 100, recherche `ILIKE` échappée ;
- **`GET /api/categories`** — la liste vient de la base (cache navigateur 5 min) ;
- `ProductApiClient` appelle ces endpoints : **seule la page demandée traverse le réseau** ;
- `Catalog.razor` : les catégories du `<select>` viennent de l'API (via `QueryClient`,
  5 min), les filtres/pagination de l'URL (`?category=&q=&page=`) deviennent la clé du
  cache (`products:page:{cat}:{q}:{page}`, 30 s).

**Limite.** Le cache `QueryClient` est par circuit/utilisateur : deux visiteurs du même
filtre font chacun une requête SQL. Acceptable à cette échelle ; le cache serveur
(`HybridCache`, tag `catalog`) couvre déjà `/api/products`.

## Stock transactionnel : l'UPDATE conditionnel

Le cœur de la phase : garantir **stock ≥ 0 sous concurrence** sans verrou applicatif.

```sql
UPDATE products SET "Stock" = "Stock" - @quantity
WHERE "Id" = @id AND "Stock" >= @quantity
RETURNING "Stock";
```

- **Atomique** : PostgreSQL évalue la condition et l'écriture dans la même instruction —
  deux achats simultanés du dernier exemplaire : un seul gagne ;
- **`RETURNING`** : le nouveau stock revient dans le même aller-retour, prêt à être
  publié sur le hub SignalR (tableau de bord temps réel, module 6) ;
- exécuté en **ADO.NET sur la connexion du contexte** (`EfProductService.TryDecrementStockAsync`).

**Piège.** EF Core ne sait pas composer du LINQ (`FirstOrDefaultAsync`) sur un
`UPDATE ... RETURNING` passé à `Database.SqlQuery<T>` : il l'embarque comme sous-requête
d'un `SELECT`, SQL invalide. D'où l'`ExecuteScalarAsync` ADO.NET (et la gestion
explicite de l'ouverture/fermeture de la connexion selon son état initial).

Endpoint **`POST /api/stock/decrement`** `{productId, quantity}` :

| Cas | Statut |
|---|---|
| Décrément appliqué | **204** + invalidation cache `catalog` + publication SignalR |
| Stock insuffisant | **409** Conflict (produit existant, distingué via `ExistsAsync`) |
| Produit inconnu | **404** |
| Quantité ≤ 0 | **400** |

C'est la brique que le tunnel de commande (phase 3) appellera pour chaque ligne du panier.

**Test de concurrence réel** : produit mis à stock 1, puis 4 requêtes parallèles de
décrément 1 → exactement **un 204**, trois 409, stock final **0** (jamais -1).

## Vérifications effectuées

- Build solution : 0 erreur, 0 avertissement ; tests d'architecture : **14/14** ;
- migration appliquée sur la base existante (données conservées, backfill vérifié en SQL) ;
- API : filtre catégorie (`total: 60`), recherche ∩ catégorie (`casque` ∩ Audio = 20),
  pagination bornée ;
- décrément : 204/409/404/400 + course parallèle ci-dessus ;
- UI : `/fr/catalog` affiche les catégories de la base, `?category=Audio` → 5 pages,
  `?category=Audio&q=casque` → 2 pages — compteurs exacts.

## Bilan des notions

- relation réelle + migration avec backfill SQL plutôt que `UpdateData` en masse ;
- le Domain reste stable : la jointure est un détail de persistance (projection) ;
- filtres et pagination appartiennent au serveur (SQL) ; l'URL reste la source de vérité
  des filtres côté UI ;
- l'UPDATE conditionnel est la réponse standard au stock négatif : pas de verrou, pas de
  retry, une seule instruction ;
- EF ne compose pas de LINQ sur du SQL d'écriture : ADO.NET sur la connexion du contexte.

**Prochaine phase** : commandes (`Order`/`OrderLine`, création depuis le panier avec
clé d'idempotence, prix recalculés serveur, emails de confirmation).
