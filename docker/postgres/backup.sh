#!/bin/sh
# ShopPulse — sauvegarde périodique de la base PostgreSQL.
#
# Deux modes :
#   sp-backup.sh          boucle infinie : une sauvegarde, puis une toutes les
#                         BACKUP_INTERVAL secondes (mode du service compose « backup »)
#   sp-backup.sh --once   une seule sauvegarde puis sortie (déclenchement manuel :
#                         docker compose exec backup sh /usr/local/bin/sp-backup.sh --once)
#
# Configuration par variables d'environnement :
#   PGHOST / PGPORT / PGUSER / PGPASSWORD / PGDATABASE  (standard libpq)
#   BACKUP_DIR        répertoire de sortie            (défaut : /backups)
#   BACKUP_RETENTION  nombre de sauvegardes conservées (défaut : 7)
#   BACKUP_INTERVAL   secondes entre deux sauvegardes  (défaut : 86400 = 24 h)
set -eu

BACKUP_DIR="${BACKUP_DIR:-/backups}"
BACKUP_RETENTION="${BACKUP_RETENTION:-7}"
BACKUP_INTERVAL="${BACKUP_INTERVAL:-86400}"

dump_once() {
    ts="$(date +%Y%m%d-%H%M%S)"
    final="$BACKUP_DIR/shoppulse-$ts.dump"
    tmp="$final.tmp"

    # Écriture dans un fichier temporaire puis renommage atomique : une
    # sauvegarde interrompue (kill, panne) ne laisse jamais un .dump tronqué
    # qu'une restauration confondrait avec une archive valide.
    pg_dump --format=custom --file="$tmp"
    mv "$tmp" "$final"

    # Vérification d'intégrité immédiate : une archive illisible est détectée
    # maintenant, pas le jour de la restauration.
    pg_restore --list "$final" > /dev/null

    echo "[backup] $(date '+%Y-%m-%d %H:%M:%S') $final ($(du -h "$final" | cut -f1))"
    apply_retention
}

apply_retention() {
    # Conserve les BACKUP_RETENTION archives les plus récentes, supprime le reste.
    count=0
    for f in $(ls -1t "$BACKUP_DIR"/shoppulse-*.dump 2>/dev/null); do
        count=$((count + 1))
        if [ "$count" -gt "$BACKUP_RETENTION" ]; then
            echo "[backup] rétention : suppression de $f"
            rm -f "$f"
        fi
    done
}

mkdir -p "$BACKUP_DIR"

if [ "${1:-}" = "--once" ]; then
    dump_once
    exit 0
fi

echo "[backup] démarrage : base $PGDATABASE sur $PGHOST, intervalle ${BACKUP_INTERVAL}s, rétention $BACKUP_RETENTION"
while true; do
    dump_once
    sleep "$BACKUP_INTERVAL"
done
