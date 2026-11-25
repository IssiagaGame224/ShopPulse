# 30 — CD : la chaîne livre, pas seulement vérifie

> **Contexte.** La CI (docs/27) vérifie chaque PR sur machine neutre. Mais jusqu'ici,
> « livrer » voulait dire « reconstruire les images sur la machine cible » — lenteur,
> dépendance au contexte local (le build Docker a d'ailleurs échoué une fois à cause
> des `bin-local/`, docs/24). Cette évolution publie les images sur **GHCR**
> (GitHub Container Registry) à chaque merge : la cible n'a plus qu'à tirer.

## Le déclencheur : jamais d'image depuis un commit rouge

```yaml
on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
    branches: [main]
  workflow_dispatch:
```

La CD ne s'exécute **qu'après un run CI réussi sur `main`** (`workflow_run` +
condition `conclusion == 'success'`), et elle cible **le SHA validé par la CI**
(`workflow_run.head_sha`), pas « le dernier main » — si deux merges se suivent, chaque
image correspond exactement à un commit vérifié. `workflow_dispatch` permet un
lancement manuel (reprise, test).

## Deux images, deux tags, un cache

Matrice `api`/`web` (deux Dockerfiles existants), `docker/build-push-action` :

- **tags** : `ghcr.io/<owner>/shoppulse-<service>:<sha>` (traçabilité — on peut
  déployer exactement le commit voulu) et `:latest` (le point d'entrée du compose) ;
- **cache** `type=gha` par service : les runs suivants réutilisent les couches ;
- **authentification** : le `GITHUB_TOKEN` du run suffit (`permissions:
  packages: write`) — aucun secret à créer ;
- **piège GHCR** : les noms d'images doivent être en **minuscules**, or le nom du
  compte GitHub peut ne pas l'être (`IssiagaGame224`) — conversion dans le workflow
  (`${GITHUB_REPOSITORY_OWNER,,}`), sinon le push échoue en « invalid tag ».

## Consommer les images : une surcharge compose

`docker/compose.images-override.yml` remplace la source des services `api`/`web`
(build local → image GHCR), le reste de la pile est inchangé :

```bash
docker compose -f docker-compose.yml -f docker/compose.images-override.yml pull
docker compose -f docker-compose.yml -f docker/compose.images-override.yml up -d
```

Épingler une version : remplacer `:latest` par le SHA d'un commit — la traçabilité
tag → commit → run CI est complète.

## Vérifications effectuées

- syntaxe du workflow et fusion compose validées localement
  (`docker compose config --images` montre bien les images GHCR pour api/web) ;
- le premier run CD réel a lieu **après merge** (le déclencheur `workflow_run` ne
  s'active que sur `main`) — vérifié à ce moment : images visibles dans l'onglet
  Packages du dépôt, `pull` fonctionnel.

> **Limite** : un workflow `workflow_run` ne peut pas être testé depuis une branche
> de PR — la validation de bout en bout se fait au premier merge (et se corrige en
> follow-up si besoin). C'est le prix d'un déclencheur qui garantit « jamais depuis
> un commit rouge ».

## Bilan

- CI + CD forment la chaîne complète : PR vérifiée → merge → image publiée,
  **sans intervention** et sans pouvoir publier du rouge ;
- le tag par SHA rend chaque déploiement **reproductible et auditable** ;
- le `GITHUB_TOKEN` suffit : pas de secret longue durée à gérer pour GHCR.

**Limites** (volontaires) : pas de déploiement automatique vers un serveur (la CD
publie, elle n'installe pas — il n'y a pas de serveur cible dans ce projet) ; images
privées par défaut (le rendre public est un choix à faire dans les paramètres du
package) ; pas de scan de vulnérabilités d'image (Trivy serait le complément naturel) ;
`latest` n'est pas signé (cosign = évolution).
