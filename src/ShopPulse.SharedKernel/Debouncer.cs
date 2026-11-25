namespace ShopPulse.SharedKernel;

/// <summary>Debounce : n'exécute l'action que lorsque les appels se sont arrêtés pendant `delay`.</summary>
public sealed class Debouncer : IDisposable
{
       private CancellationTokenSource? _cts;

       public async Task RunAsync(Func<CancellationToken, Task> action, TimeSpan delay)
       {
              _cts?.Cancel();
              _cts?.Dispose();
              _cts = new CancellationTokenSource();
              var token = _cts.Token;

              try
              {
                     await Task.Delay(delay, token);
                     await action(token);
              }
              catch (OperationCanceledException) { /* remplacé par une saisie plus récente */ }
       }

       public void Dispose() { _cts?.Cancel(); _cts?.Dispose(); }
}
