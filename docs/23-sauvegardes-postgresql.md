# 23 — Sauvegardes PostgreSQL : dumps périodiques, rétention et restauration vérifiée

> **Contexte.** Toutes les données de la boutique — catalogue, commandes, comptes,
> outbox — vivent dans un seul volume Docker (`pgdata`). Une boutique « production-ready »
> sans sauvegarde, c'est une boutique à un incident du néant : corruption du volume,
> `docker compose down -v` malheureux, migration ratée. Cette évolution ajoute un service
> de sauvegarde périodique **et prouve que les archives sont restaurables** — une
> sauvegarde qu'on n'a jamais restaurée n'est qu'une hypothèse.

## Le service `backup` : pg_dump en boucle, sans cron

Un conteneur dédié dans `docker-compose.yml`, basé sur la même image `postgres:17-alpine`
que la base (même version de `pg_dump` que le serveur — voir « Piège » plus bas) :

```yaml
backup:
  image: postgres:17-alpine
  command: sh /usr/local/bin/sp-backup.sh
  environment:
    PGHOST: db
    PGDATABASE: shoppulse
    BACKUP_INTERVAL: "86400"   # 24 h
    BACKUP_RETENTION: "7"
  volumes:
    - ./docker/postgres/backup.sh:/usr/local/bin/sp-backup.sh:ro
    - pgbackups:/backups
  depends_on:
    db:
      condition: service_healthy
```

Le script (`docker/postgres/backup.sh`) fait une première sauvegarde au démarrage puis
boucle avec `sleep` — pas besoin de cron dans un conteneur : la boucle *est* la
planification, et les journaux du service (`docker compose logs backup`) racontent
chaque exécution. Le mode `--once` permet le déclenchement manuel :

```bash
docker compose exec backup sh /usr/local/bin/sp-backup.sh --once
```

## Trois décisions qui font la différence entre « dump » et « sauvegarde »

**1. Format custom (`pg_dump --format=custom`), pas SQL brut.** L'archive est compressée
et **partielle** : `pg_restore` permet de ne restaurer qu'une table, qu'un schéma, et
autorise `--clean` / `--no-owner`. Un dump SQL texte ne se rejoue qu'en entier.

**2. Écriture atomique.** Le dump est écrit dans `shoppulse-<horodatage>.dump.tmp` puis
renommé. Un `pg_dump` interrompu (kill, panne, `docker compose down` au mauvais moment)
ne laisse jamais un `.dump` tronqué qu'une restauration confondrait avec une archive
valide — le `.tmp` orphelin est simplement ignoré.

**3. Vérification immédiate.** Juste après l'écriture, `pg_restore --list` relit le
sommaire de l'archive : si le fichier est illisible, on le sait **maintenant**, dans les
journaux du service — pas le jour où on en a besoin.

La **rétention** (7 archives par défaut) supprime les plus anciennes à chaque dump :
le volume `pgbackups` a une taille bornée, pas de disque plein en silence.

## Restauration : la procédure, vérifiée de bout en bout

```bash
# 1. Lister les archives disponibles
docker compose exec backup ls -l /backups

# 2. Créer une base de destination (jamais de restauration « à chaud » sur la base vivante)
docker compose exec db psql -U shoppulse -d postgres \
  -c "CREATE DATABASE shoppulse_restore;"

# 3. Restaurer depuis le conteneur backup (pg_restore se connecte via le réseau compose)
docker compose exec backup sh -c \
  'pg_restore -h db -U shoppulse -d shoppulse_restore --no-owner /backups/shoppulse-XXXXXX.dump'

# 4. Vérifier, puis basculer (renommage des bases) ou jeter
docker compose exec db psql -U shoppulse -d shoppulse_restore -c "SELECT count(*) FROM orders;"
```

**Vérifications effectuées** (base réelle du projet, 25/11/2026) :

- dump initial au démarrage du service : `shoppulse-20261125-140506.dump` (36 Ko) ;
- déclenchement manuel `--once` : OK ;
- rétention forcée à 2 (`docker compose exec -e BACKUP_RETENTION=2 … --once`) : les
  archives excédentaires sont supprimées, les 2 plus récentes conservées ;
- **restauration complète dans `shoppulse_restore`** : comptages identiques à la base
  d'origine sur les 5 tables — products 202/202, categories 3/3, orders 122/122,
  order_lines 124/124, outbox_messages 114/114 — et l'historique des migrations EF
  (7 entrées) est présent, donc l'application peut redémarrer dessus sans ré-appliquer
  quoi que ce soit.

> **Piège** (Windows / Git Bash) : `docker compose exec backup sh /usr/local/bin/sp-backup.sh`
> échoue avec `can't open 'C:/Program Files/Git/usr/local/bin/…'` — Git Bash convertit
> les chemins Unix en chemins Windows avant de les passer à Docker. Correction :
> `export MSYS_NO_PATHCONV=1` avant la commande (PowerShell et cmd ne sont pas concernés).

> **Piège** : utiliser une image `postgres` d'une autre majeure pour le conteneur de
> sauvegarde. `pg_dump` refuse de sauvegarder un serveur **plus récent** que lui
> (« server version mismatch »). Même image, même tag que le service `db`.

## Bilan

- une sauvegarde = un dump **vérifiable** (relecture immédiate), **atomique** (tmp + mv)
  et **borné** (rétention) — les trois propriétés sont dans le script, pas dans la bonne
  volonté de l'opérateur ;
- la restauration est une procédure **documentée et exercée**, pas théorique : les
  comptages prouvent l'équivalence ;
- le coût est d'un conteneur quasi idle (une connexion par jour).

**Limites** (volontaires, à traiter en production) :

- **Pas de PITR** : les dumps sont des photographies quotidiennes ; la perte maximale
  est de 24 h de données. Le point-in-time recovery exige l'archivage continu des WAL
  (`archive_command`, ou un outil comme WAL-G / pgBackRest).
- **Même hôte** : `pgbackups` vit sur la même machine que `pgdata` — la règle 3-2-1
  (3 copies, 2 supports, 1 hors site) n'est pas respectée ; il manque la copie externe
  (objet S3, autre machine).
- **Pas de chiffrement** des archives ni de supervision de l'échec de sauvegarde
  (l'alerting de la phase suivante pourra surveiller l'ancienneté du dernier dump).
- La restauration est **manuelle** : c'est un choix — une restauration automatique sur
  base vivante est une arme chargée.
