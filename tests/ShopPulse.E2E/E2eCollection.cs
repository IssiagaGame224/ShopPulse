namespace ShopPulse.E2E;

/// <summary>Toutes les classes E2E partagent cette collection : xUnit les exécute
/// SÉQUENTIELLEMENT. En parallèle, 4+ navigateurs Chromium et leurs circuits SignalR
/// se disputaient les ressources — les circuits mettaient trop de temps à se connecter
/// et les clics pré-circuit échouaient par intermittence (faux échecs en rafale).
/// La suite complète reste sous la minute.</summary>
[CollectionDefinition("e2e")]
public sealed class E2eCollection;
