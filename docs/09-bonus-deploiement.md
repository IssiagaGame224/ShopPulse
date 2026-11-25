# Chapitre 9 — Bonus : Déploiement (Docker Compose + publication Release)

> **Question du chapitre :** comment faire tourner ShopPulse « comme en production » sur une machine de développement, et que change le passage en Release ?
> **Livrables vérifiés** : `docker-compose.yml` (Keycloak + API + Web), Dockerfiles multi-étages, publication Release mesurée.

## 1. Les trois artefacts

```
docker-compose.yml            # l'orchestration : keycloak, api, web
src/ShopPulse.Api/Dockerfile  # build SDK → runtime, ICU disponible
src/ShopPulse.Web/Dockerfile  # idem
.dockerignore                 # bin/obj/docs/hors de l'image
```

```bash
docker compose up -d --build   # construit et démarre toute la pile
docker compose logs -f web     # suit les journaux
docker compose down            # arrête et nettoie
```

Chaque Dockerfile suit le même contrat : **build multi-étage** (SDK pour restaurer/publier, `aspnet:10.0` pour l'exécution), restauration par couches (fichiers `.csproj` d'abord, code ensuite — le cache Docker ne se invalide qu'au changement de code), et `ASPNETCORE_HTTP_PORTS=8080` en conteneur.

## 2. Le réseau : la leçon de ce chapitre

Trois acteurs veulent trois adresses différentes pour le même Keycloak :

| Qui | Vers quoi | Adresse |
|---|---|---|
| Le navigateur | Keycloak (formulaire de login) | `http://localhost:8080` |
| Le conteneur Web | métadonnées OIDC | `http://keycloak:8080` (réseau compose) |
| Le jeton | son émetteur (iss) | `http://localhost:8080/realms/shoppulse` |

C'est le **DNS split-horizon**, piège classique d'OIDC en conteneurs. La solution en trois réglages (`docker-compose.yml`) :

1. **Keycloak** : `--hostname=http://localhost:8080` → l'émetteur des jetons est stable et public, quel que soit le réseau d'origine.
2. **Web** : `Oidc__Authority=http://keycloak:8080/realms/shoppulse` → les métadonnées sont chargées par le réseau interne.
3. **Web** : `Oidc__ValidIssuer=http://localhost:8080/realms/shoppulse` → la validation d'émetteur compare au *public*, pas à l'interne.

Côté code, les deux réglages sont configurables et à défaut stricts (`Program.cs`) :

```csharp
if (builder.Configuration["Oidc:ValidIssuer"] is { } validIssuer)
       o.TokenValidationParameters.ValidIssuer = validIssuer;
o.RequireHttpsMetadata = builder.Configuration.GetValue("Oidc:RequireHttpsMetadata",
       !builder.Environment.IsDevelopment());
```

> **Règle d'or** : le relâchement (`RequireHttpsMetadata=false`, justifié ici par Keycloak `start-dev` en clair) doit être **explicite et documenté** — jamais un défaut.

L'API, elle, n'a qu'un public : le conteneur Web, via `Api__BaseUrl=http://api:8080/` — le nom de service compose fait office de DNS interne.

## 3. Ce que la publication Release change (et prouve)

```bash
dotnet publish src/ShopPulse.Web/ShopPulse.Web.csproj -c Release -o out
ASPNETCORE_ENVIRONMENT=Production dotnet out/ShopPulse.Web.dll --urls https://localhost:5001
```

Mesuré sur le fil rouge (serveur de test sur le port 5080, `Api:BaseUrl` pointant l'API de dev) :

| En-tête | Development | **Release/Production** |
|---|---|---|
| `Cache-Control` sur `app.<empreinte>.css` | `no-cache` | **`max-age=31536000, immutable`** |
| `Content-Security-Policy-Report-Only` | présent | présent |
| `Server` | masqué | masqué |
| `Strict-Transport-Security` | absent | présent **derrière un terminateur TLS** (aucun port HTTPS configuré seul ne l'active ; voir les en-têtes transférés du module 7) |
| Exceptions | page de développeur | gestionnaire d'erreurs générique |

L'exercice 1 du module 5 (« vérifier que `app.<hash>.css` porte `max-age` d'un an ») n'est tenable qu'ici : en Development, `MapStaticAssets` sert tout en `no-cache` pour ne jamais masquer une modification.

## 4. Pièges vécus lors de ce bonus

1. **Hôte minimal et ports** : `docker compose up` échoue si les serveurs de dev occupent déjà 5026/5299/8080 — `docker compose down` des deux mondes ne se mélangent pas, mais les *ports* si.
2. **`RequireHttpsMetadata` en Production** : l'application publiée refuse *toute* requête si l'autorité OIDC est en clair — c'est la sécurité qui fait son travail (module 7). D'où le réglage explicite par configuration, jamais par défaut.
3. **ICU et cultures** (rappel module 8) : l'image `aspnet:10.0` inclut ICU — les cultures fr/en/ar fonctionnent en conteneur. Sur une image `runtime-deps` minimale, il faudrait installer `libicu` ou toutes les cultures retomberaient en invariant.
4. **Démarrage à froid** : la première exécution d'une application publiée (JIT) peut prendre 30-60 s avant le premier `listen` — ne pas conclure trop vite à un échec.
5. **DNS split-horizon, niveau 2** (Keycloak) : `--hostname=localhost:8080` stabilise l'émetteur mais casse les URLs *internes* du document de découverte (JWKS pointant vers `localhost`, inaccessible depuis le conteneur). Il faut **aussi** `--hostname-backchannel-dynamic=true` : les URLs de backchannel suivent l'hôte de la requête, l'émetteur reste public. Symptôme typique : `IDX20807: Unable to retrieve document from 'http://localhost:8080/.../certs'` *à l'intérieur* du conteneur.
6. **`invalid_client_credentials` au PAR** (vérifié dans les événements Keycloak) : les **user-secrets n'existent pas dans le conteneur**. Le secret de dev doit être fourni par variable d'environnement (`Oidc__ClientSecret`) — en production, un magasin de secrets (Key Vault).
7. **Binding de port corrompu** : un premier `up` interrompu par un conflit de ports (8080 déjà pris par l'ancien conteneur Keycloak) laisse un conteneur avec un binding invalide (`invalid IP`) réutilisé tel quel au `up` suivant. `docker compose down` complet avant de recommencer.
8. **`--no-restore` au publish : l'interactivité silencieusement absente** (le plus sournois du bonus). Le Dockerfile restaurait les `.csproj` seuls, puis publiait avec `--no-restore` : la compilation réussissait, mais `wwwroot/_framework/` (dont `blazor.web.js`) était **absent de l'image** — les assets statiques du framework ne sont découverts que lors de la restauration complète du contexte de publication. Symptôme : la page se rend côté serveur (le HTML complet arrive), mais aucun circuit ne démarre, le streaming ne se termine jamais, les boutons sont morts — et `@Assets` émet alors l'URL *logique* non empreintée, qui n'existe pas en production. Diagnostic : l'onglet Réseau du navigateur montre le script `blazor.web.js` en 404 (`MIME type text/html`). Correctif : laisser `dotnet publish` restaurer (`--no-restore` retiré). Leçon générale : **un build qui réussit ne prouve pas un déployable qui fonctionne — il faut un test fumée qui clique**.
9. **Clés Data Protection éphémères** : par défaut, le conteneur stocke ses clés dans un dossier jetable (`/root/.aspnet/DataProtection-Keys`) — chaque redémarrage invalide les jetons antiforgery déjà émis (erreurs « key not found in the key ring »). Un volume nommé persiste les clés entre redémarrages ; en vraie production : magasin partagé et chiffré (module 7).

## 5. Vérification de la pile (les trois services)

```bash
curl http://localhost:5299/api/products                       # API : 200
curl http://localhost:8080/realms/shoppulse/.well-known/openid-configuration   # Keycloak : 200
curl -s http://localhost:5026/fr/catalog | grep '<html'       # Web : lang="fr" dir="ltr"
curl -s http://localhost:5026/ar/catalog | grep '<html'       # Web : lang="ar" dir="rtl"
```

Et le test complet reste celui du module 7 : `/login` → Keycloak → `demo` / `demo1234` → `/admin/stock`, cette fois avec **tous les services en conteneurs** et l'application en environnement Production.

## 6. Vers la vraie production

Ce qu'un déploiement réel ajouterait, dans l'ordre : un reverse proxy qui termine TLS (Nginx, YARP, Front Door) avec les en-têtes transférés restreints au proxy connu ; HTTPS sur Keycloak (`start-dev` n'est pas un mode production — TLS, base de données externe, `KC_HOSTNAME` strict) ; des secrets dans un magasin (Key Vault) plutôt que des variables d'environnement ; un magasin de tickets pour le cookie si le trafic grimpe ; l'affinité de session ou Azure SignalR Service (module 4, concept Scalabilité) ; et la surveillance OpenTelemetry des circuits (module 2, concept RUM).
