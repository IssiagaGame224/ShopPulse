# 27 — CI GitHub Actions : la vérification ne dépend plus de la machine

> **Contexte.** Le workflow d'équipe (AGENTS.md §9) exige une PR validée avant chaque
> merge — mais « ça passe chez moi » ne suffit pas : la session précédente l'a prouvé
> deux fois (culture de l'OS masquant un bug i18n, ports fantômes bloquant les
> vérifications locales). Ce chapitre ajoute l'intégration continue : chaque PR est
> vérifiée par un runner GitHub **identique pour tout le monde**, et les statuts
> deviennent requis pour fusionner.

## Le pipeline : deux jobs, une question chacun

`.github/workflows/ci.yml`, déclenché sur PR vers `main` et push sur `main`
(avec `concurrency` : un nouveau push annule le run précédent de la même branche).

**Job 1 — Build + tests rapides (~2 min)**
1. `actions/setup-dotnet` 10.0.x ;
2. build Release (avertissements = erreurs, NuGetAudit bloquant — `Directory.Build.props`) ;
3. tests d'architecture NetArchTest (14) ;
4. Keycloak en conteneur (`docker compose up -d keycloak` + sondage de la découverte
   OIDC) puis tests d'intégration (16) : WebApplicationFactory + **Testcontainers**
   PostgreSQL — Docker est natif sur les runners, les conteneurs éphémères fonctionnent
   sans configuration.

**Job 2 — E2E + A11y (~3 min)**
1. build Release ;
2. **pile complète** : `docker compose up -d --build db keycloak mailpit api web` —
   les mêmes images qu'en production-like, les mêmes ports (5026/5299/8080/8025) ;
3. sondage de disponibilité (`/health/live` + `/fr`) avec dump des journaux conteneurs
   en cas de timeout ;
4. `playwright.ps1 install --with-deps chromium` (les tests A11y réutilisent le même
   navigateur) ;
5. suites E2E (7 tests) et axe-core (5 tests) ;
6. en cas d'échec : `docker compose logs` dans le journal du job.

## Autopsie : le health check qui regardait la mauvaise base

**Symptôme.** Premier run : 15/16 tests d'intégration verts, mais
`Health_IsHealthy_WithAllDependencies` échoue en **503** — uniquement en CI, jamais
en local.

**Diagnostic.** Le health check capturait sa chaîne de connexion **au moment de
l'enregistrement** :

```csharp
// AVANT — la valeur est lue PENDANT l'exécution de Program.cs
.AddNpgSql(builder.Configuration.GetConnectionString("ShopPulse")!, …)
```

Or `WebApplicationFactory` applique ses surcharges de configuration (la base
Testcontainers) **au `Build()`**, après la lecture. L'EF Core, résolu paresseusement
via DI, voyait la bonne base (d'où 15 tests verts) ; le health check interrogeait
`localhost:5432` — qui, **en local, tombe sur la base compose** : le bug était
invisible sur le poste de développement et systématique sur un runner vierge.

**Correctif** : résolution paresseuse, comme EF —

```csharp
.AddNpgSql(sp => sp.GetRequiredService<IConfiguration>()
                    .GetConnectionString("ShopPulse")!, name: "postgresql")
```

> **Piège** : avec le minimal hosting, tout ce qui est **lu directement** dans
> `Program.cs` (au lieu d'être résolu via DI) ignore les surcharges de configuration
> des tests. Règle : une valeur de configuration consommée par un service se lit
> **via `IServiceProvider`**, jamais en capturant `builder.Configuration` — et un
> test qui passe « parce que la bonne base est sur le même port en local » est un
> faux vert.

## Vérifications effectuées

- run 1 (PR #2) : E2E + A11y **verts du premier coup** (2 min 53) ; build rouge sur
  le health check → autopsie ci-dessus ;
- run 2 après correctif : les deux jobs verts ;
- les deux statuts (`Build + tests rapides`, `E2E + A11y (pile complète)`) ajoutés
  aux **checks requis** de la protection de `main` : une PR ne peut plus être
  fusionnée si la CI est rouge.

## Bilan

- la CI est le **même rituel de vérification**, exécuté sur une machine neutre :
  ce qui passait « par accident » en local (ports, culture, base voisine) échoue
  ici — et c'est le but ;
- les conteneurs rendent le pipeline trivial : pas d'installation PostgreSQL,
  Keycloak ou Mailpit sur le runner, `docker compose up` suffit ;
- le premier bug attrapé par la CI (health check) valide l'investissement dès le
  premier run.

**Limites** (volontaires) : pas de cache NuGet/Docker (runs ~5 min au total —
acceptable, à optimiser si la fréquence augmente) ; le chemin Stripe réel reste
non testé (pas de clés, cf. docs/17) ; les sauvegardes et l'alerting (services
compose additionnels) ne sont pas exercés en CI ; pas de déploiement automatique —
la CI vérifie, elle ne livre pas encore.
