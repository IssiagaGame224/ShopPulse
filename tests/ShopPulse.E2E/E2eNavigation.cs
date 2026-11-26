using Microsoft.Playwright;

namespace ShopPulse.E2E;

/// <summary>Navigation améliorée en conditions lentes : un clic peut être absorbé
/// si le circuit SignalR peine (runner CI à froid, machine chargée) — le HTML
/// prerendu ment sur l'interactivité. Stratégie unique du projet : cliquer,
/// vérifier l'EFFET (sélecteur attendu), recommencer. Voir docs/28 et docs/33.</summary>
internal static class E2eNavigation
{
       public static async Task ClickUntilAsync(
              IPage page, string clickSelector, string waitSelector,
              int attempts = 4, float timeoutMs = 5000)
       {
              for (var attempt = 0; ; attempt++)
              {
                     await page.ClickAsync(clickSelector);
                     try
                     {
                            await page.WaitForSelectorAsync(waitSelector,
                                   new PageWaitForSelectorOptions { Timeout = timeoutMs });
                            return;
                     }
                     catch (TimeoutException) when (attempt < attempts - 1)
                     {
                            // Clic absorbé (circuit pas prêt) : on recommence.
                     }
              }
       }
}
