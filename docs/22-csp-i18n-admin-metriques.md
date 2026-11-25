# 22 — CSP appliquée, back-office trilingue, métriques OpenTelemetry

> **Contexte.** Dernier lot « production » : la CSP sort du Report-Only et bloque
> réellement, le back-office parle les trois langues, et les services exposent leurs
> métriques au format Prometheus.

## 1. Content-Security-Policy appliquée

L'en-tête passe de `Content-Security-Policy-Report-Only` à **`Content-Security-Policy`**
(bloquant). Préparation nécessaire :

- le JSON-LD de la fiche produit (script inline) reçoit le **nonce** de la requête
  (`HttpContext.Items["csp-nonce"]`, posé par le middleware du module 7) ;
- l'inventaire des scripts inline du projet se limite à deux : le service worker
  (nonce existant) et le JSON-LD.

La politique : `script-src 'self' 'nonce-…' https://js.stripe.com` ;
`connect-src 'self' ws: wss: https://api.stripe.com` (circuit SignalR + Stripe) ;
`frame-src` pour les iframes 3DS ; `frame-ancestors 'none'` ; `object-src 'none'`.
`style-src 'unsafe-inline'` reste assumé (bootstrap) — à resserrer avec des hashes.

**Vérifié en navigateur réel** : E2E **5/5** et axe **5/5** sous CSP bloquante —
parcours d'achat complet, circuit SignalR, scripts à nonce : rien de légitime n'est
bloqué.

**Piège de build rencontré** : une erreur de compilation dans une RCL
(`IHttpContextAccessor` non importé) faisait échouer le build… mais un `tail -2`
trop court masquait la ligne d'erreur, et le serveur redémarré servait l'ancienne
DLL. Leçon : toujours vérifier le compteur d'erreurs ET l'horodatage de la DLL.

## 2. Back-office trilingue

40 clés × fr/en/ar : pages commandes, produits, édition produit, navigation et
libellés de statuts (partagés avec les pages compte). Le back-office suit la langue
de l'URL comme le reste de la boutique.

## 3. Métriques OpenTelemetry

`AddOpenTelemetry().WithMetrics(...)` dans les deux services :
instrumentation **ASP.NET Core** (requêtes entrantes) et **HttpClient** (sortantes),
export **Prometheus** sur `GET /metrics` :

```
http_server_request_duration_seconds_count{http_request_method="GET",
  http_response_status_code="200", http_route="/api/products", …} 1
```

Un Prometheus peut scraper les deux services ; les tableaux de bord (Grafana) et
l'alerting restent de l'outillage externe.

**Limite.** `/metrics` est anonyme : en production, le restreindre au réseau
interne (reverse proxy) ou par authentification.

## Vérifications effectuées

- build 0/0 ; intégration **16/16** ; **E2E 5/5** ; axe **5/5** ; archi **14/14** ;
- en-tête `Content-Security-Policy` (bloquant) vérifié ;
- `/metrics` renseigné sur l'API et le Web après trafic.

## Bilan de la série « production » (docs/19 → 22)

Sondes, logs structurés, refresh token, Outbox, CSP bloquante, i18n complet,
métriques : la boutique coche les cases d'exploitabilité d'un service réel.
Restes possibles : tracing distribué (OTel traces vers un collecteur), alerting,
hashes CSP pour les styles, sauvegardes PostgreSQL automatisées.
