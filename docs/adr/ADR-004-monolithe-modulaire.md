# ADR-004 : Monolithe modulaire plutôt que micro-frontends

- **Statut :** accepté (2026-11-17)
- **Contexte :** deux équipes (catalogue, commande), un seul cycle de livraison, besoin de frontières nettes entre le design system et les features.
- **Décision :** une Razor Class Library par fonctionnalité (`ShopPulse.Features.*`), un design system en RCL (`ShopPulse.UI`), un hôte Blazor unique qui compose les modules via `IShopModule` + `ModuleCatalog` + `AddAdditionalAssemblies`, et des tests NetArchTest qui vérifient les frontières dans la CI.
- **Conséquences :** pas de déploiement indépendant par équipe ; le couplage reste à la compilation (références de projets) plutôt qu'à l'exécution. Réévaluer si une équipe exige son propre cycle de livraison — l'échappatoire documentée est alors YARP (applications séparées derrière un reverse proxy).
