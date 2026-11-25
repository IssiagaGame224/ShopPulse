# 21 — Refresh token, Outbox et sondes liveness/readiness

> **Contexte.** Trois chantiers « production » : les sessions ne meurent plus au bout de
> 30 minutes (refresh token OIDC), les emails ne se perdent plus jamais (pattern
> Outbox), et les sondes distinguent vivacité et disponibilité.

## 1. Refresh token OIDC — la fin des sessions API muettes

Jusqu'ici (`docs/14`, limite assumée) : passée la durée de l'access token (30 min),
les appels API du circuit repartaient en **anonyme** — commandes invitées silencieuses
pour un client pourtant connecté.

- `UserTokenStore` conserve désormais **access + refresh token + expiration** par sub ;
- `GetValidTokenAsync(sub)` : token frais → tel quel ; expiré (marge de 60 s) →
  **grant `refresh_token`** contre Keycloak, une seule requête en vol (sémaphore +
  relecture du cache), entrée purgée si Keycloak refuse (reconnexion nécessaire) ;
- le handler (`AccessTokenHandler`) ne change quasiment pas : même appel, désormais async.

**Vérification réelle** : realm temporairement configuré à `accessTokenLifespan=60`,
parcours Playwright complet — login, **attente de 75 s**, achat. Preuve en base : la
commande est **liée au compte** (`UserId` non nul) malgré un token expiré depuis
15 s. Le test est conservé en **opt-in** (`SHOPPULSE_REFRESH_TEST=1` + realm à 60 s).

**Piège d'encodage** : les journaux console Windows mangent les accents — greper
« rafraîchi » ne trouve rien ; la preuve fiable est en base, pas dans les logs.

## 2. Pattern Outbox — les emails ne se perdent plus

Avant : email envoyé juste après le commit, dans un try/catch — un crash entre les deux,
ou un SMTP en panne, et l'email était **perdu** (limite documentée depuis `docs/13`).

Maintenant :

```
Transaction métier                     Worker (poll 2 s)
┌────────────────────────────┐        ┌──────────────────────────┐
│ INSERT orders …            │        │ SELECT outbox WHERE      │
│ INSERT outbox_messages ────┼───────▶│   ProcessedAt IS NULL    │
│ (même commit, tout ou rien)│        │ → envoi SMTP → ProcessedAt│
└────────────────────────────┘        │ échec → Attempts++, retry │
                                      └──────────────────────────┘
```

- `OutboxMessageEntity` (type, OrderId, Attempts, LastError), écrite dans la
  **même transaction** que la commande (`EfOrderService`) et que chaque changement
  de statut ;
- `OutboxWorker` (BackgroundService, lots de 10, ordre chronologique) : au-delà de
  5 tentatives le message reste en base pour investigation — jamais supprimé ;
- les envois directs des endpoints sont supprimés (une seule voie d'envoi).

**Démonstration de résilience** : Mailpit arrêté → commande acceptée (201), message en
attente avec `Attempts=1` et l'erreur de connexion capturée ; Mailpit relancé → email
**rejoué automatiquement**, file vide.

## 3. Sondes liveness / readiness

- `GET /health/live` : le processus répond (aucune dépendance) — pour ne pas redémarrer
  un service sain dont la base tousse ;
- `GET /health` : disponibilité réelle (JSON détaillé, `docs/19`) — readiness.

## Vérifications effectuées

- build 0/0 ; intégration **16/16** ; **E2E 5/5** (le test refresh est opt-in/no-op
  par défaut) ;
- Outbox : email via worker ✓, panne SMTP → retry → livraison ✓ ;
- refresh : commande liée au compte après expiration réelle du token ✓ (en base).

**Note flaky** : l'ajout du worker a décalé les timings E2E — le clic « Ajouter au
panier » avant la connexion du circuit devenait visible. Les tests cliquent désormais
en boucle jusqu'au badge panier à 1 (déterministe), et l'attente Mailpit est un sondage.

## Bilan

- une écriture externe non rejouable (email) se couple au métier par une table, pas
  par un try/catch ;
- le refresh token se gère côté serveur avec verrou et marge — jamais côté navigateur ;
- liveness ≠ readiness : redémarrer sur une dépendance en panne aggrave la panne.
