# 19 — Observabilité : health checks et logs structurés

> **Contexte.** Une boutique « production-ready » doit répondre à deux questions à tout
> moment : *est-ce que ça marche ?* (sondes) et *que s'est-il passé ?* (logs). Cette
> évolution ajoute les deux, côté API et côté Web.

## Health checks : `/health` sur les deux services

```json
{
  "status": "Healthy",
  "totalDurationMs": 16,
  "checks": [
    { "name": "postgresql", "status": "Healthy", "durationMs": 13 },
    { "name": "keycloak",   "status": "Healthy", "durationMs": 16 }
  ]
}
```

- **API** : `AddNpgSql` (la base répond-elle ?) + `AddUrlGroup` sur la découverte OIDC
  de Keycloak (l'IdP répond-il ?). Sans les deux, pas de boutique.
- **Web** : son propre `/health` vérifie que **l'API** répond (`Api:BaseUrl` + `health`) —
  la chaîne complète est sondable de l'extérieur : Web → API → (base, Keycloak).
- Réponse JSON détaillée (statut, durée par dépendance) via un `ResponseWriter` local à
  chaque `Program.cs` — volontairement dupliqué (15 lignes) plutôt que d'ajouter une
  référence framework ASP.NET à la classlib Infrastructure.

Un orchestrateur (Docker `healthcheck`, Kubernetes liveness/readiness, superviseur)
peut désormais sonder les deux services. **Limite** : les checks sont « tout ou rien » ;
la distinction liveness/readiness et le check SMTP restent des évolutions.

## Logs structurés Serilog

`Serilog.AspNetCore` remplace le logger par défaut dans les deux services :

- niveaux lus depuis la configuration (`Logging:LogLevel`, comme avant) ;
- enrichissement : `Application` (ShopPulse.Api / ShopPulse.Web) sur chaque événement,
  `TraceId` par requête via `UseSerilogRequestLogging` — une requête se suit de bout
  en bout, et les journaux se filtrent/aggregent (Seq, Elastic, Loki…) sans changer le code ;
- format console lisible : `[04:34:18 INF] [ShopPulse.Api] HTTP GET /api/products
  responded 200 in 42 ms`.

**Piège.** `using Serilog;` rend `ILogger` ambigu avec
`Microsoft.Extensions.Logging.ILogger` : le compilateur a lié silencieusement le
mauvais type dans une signature existante (erreurs CS1503/CS4010 en cascade). Correction :
type pleinement qualifié dans la signature concernée.

## Vérifications effectuées

- `GET /health` API → Healthy, postgresql 13 ms + keycloak 16 ms ;
- `GET /health` Web → Healthy, api 26 ms ;
- logs structurés visibles au démarrage et par requête ;
- build 0/0, architecture 14/14, **E2E 4/4** (aucune régression fonctionnelle).

## Bilan

- une sonde par service, et chaque sonde teste SES dépendances — pas une de plus ;
- les logs sont des données (champs), pas des phrases : le format console n'est qu'un
  rendu parmi d'autres ;
- les paquets `AspNetCore.HealthChecks.*` restent en majeure 9 (compatibles .NET 10),
  Serilog en 11 — versions épinglées dans `Directory.Packages.props` comme toujours.
