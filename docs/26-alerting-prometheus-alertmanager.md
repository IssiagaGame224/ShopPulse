# 26 — Alerting : Prometheus + Alertmanager, prouvé par une panne réelle

> **Contexte.** Les métriques existaient (docs/22) mais personne ne les regardait :
> une métrique sans alerte est un tableau de bord que l'on consulte **après** l'incident.
> Cette évolution ferme la boucle : Prometheus scrape `/metrics`, évalue des règles,
> et Alertmanager notifie par email — capturé par Mailpit en développement, comme les
> emails transactionnels.

## La chaîne complète

```
api:8080/metrics ─┐
                  ├─→ Prometheus (scrape 15 s, règles) ─→ Alertmanager ─→ Mailpit ─→ UI :8025
web:8080/metrics ─┘        UI :9090                        UI :9093
```

Trois fichiers de configuration, montés en volume (aucune image à construire) :

- `docker/prometheus/prometheus.yml` — deux jobs de scraping (`shoppulse-api`,
  `shoppulse-web`) + l'auto-scrape de Prometheus ; pointe l'Alertmanager.
- `docker/prometheus/alert.rules.yml` — trois alertes, une par famille de question :
  - **`ServiceDown`** (`up == 0` pendant 1 min, *critical*) — le service ne répond plus ;
  - **`HighHttp5xxRate`** (> 5 % de 5xx sur 5 min, *warning*) — régression applicative
    ou dépendance en échec ; le ratio se calcule sur
    `http_server_request_duration_seconds_count` (le label `http_response_status_code`
    vient de l'instrumentation OTel, docs/22) ;
  - **`SlowRequestsP95`** (`histogram_quantile(0.95, …) > 1 s` pendant 10 min,
    *warning*) — lenteur perceptible.
- `docker/alertmanager/alertmanager.yml` — routage groupé (`group_by alertname, job`,
  `group_wait 10s`, rappel toutes les 15 min) vers un receiver email dont le
  **smarthost est Mailpit** (`mailpit:1025`) : en dev, aucune alerte ne sort, tout se
  lit dans l'UI Mailpit. `send_resolved: true` notifie aussi le retour à la normale.

## Vérification : on éteint la boutique pour voir l'alarme sonner

Une alerte qu'on n'a jamais vue se déclencher n'est qu'une hypothèse (le même principe
que les sauvegardes de docs/23). Test de bout en bout :

1. `docker compose stop web` à 18:02:19 — Prometheus passe la cible `shoppulse-web`
   à `down` au scrape suivant ;
2. après `for: 1m` + `group_wait: 10s`, l'alerte est **active** dans l'API
   Alertmanager (`/api/v2/alerts` : `ServiceDown shoppulse-web, état=active`) ;
3. Mailpit reçoit : **`[FIRING:1] ServiceDown shoppulse-web (web:8080 critical)`** ;
4. `docker compose up -d web` → email **[RESOLVED]** à la résolution.

Les trois règles sont chargées (`/api/v1/rules` : état `inactive` en fonctionnement
normal) et les trois cibles `up`.

> **Piège** : les noms de métriques OTel exportés en Prometheus suivent la convention
> `snake_case` avec unité (`http_server_request_duration_seconds`, pas
> `http.server.request.duration`) — écrire les règles **après** avoir lu `/metrics`,
> pas depuis la documentation OTel.

> **Piège** : le `resolve_timeout` d'Alertmanager (5 min par défaut) retarde l'email
> RESOLVED — ce n'est pas un oubli de notification.

## Bilan

- une alerte = une **question opérationnelle** (« le service répond-il ? », « sert-il
  des erreurs ? », « est-il lent ? »), pas une métrique recopiée ;
- la notification passe par la même infrastructure que les emails transactionnels
  (Mailpit) : zéro secret, zéro service externe en dev ;
- les seuils (5 %, 1 s, 1 min) sont des points de départ documentés, à ajuster sur
  l'expérience réelle — une alerte qui crie trop souvent finit ignorée.

**Limites** (volontaires) : pas d'alerte **métier** (stock bas, commandes en échec de
paiement, ancienneté du dernier dump de sauvegarde — nécessiterait des métriques
custom) ; pas de traces OTel dans Prometheus (les traces sont dans Jaeger, docs/25) ;
un seul receiver email (Slack/PagerDuty = un bloc `receivers` de plus) ; Prometheus et
Alertmanager sans authentification (à protéger par le réseau, comme `/metrics`).
