using Microsoft.EntityFrameworkCore;
using ShopPulse.Application;
using ShopPulse.Infrastructure.Persistence;

namespace ShopPulse.Api;

/// <summary>Worker Outbox : traite les emails en attente (poll toutes les 2 s, par lots
/// de 10, dans l'ordre). Un échec SMTP incrémente Attempts et réessaie au passage suivant ;
/// au-delà de 5 tentatives, le message reste en base pour investigation — jamais perdu.</summary>
public sealed class OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger)
       : BackgroundService
{
       private const int MaxAttempts = 5;

       protected override async Task ExecuteAsync(CancellationToken stoppingToken)
       {
              while (!stoppingToken.IsCancellationRequested)
              {
                     try
                     {
                            await ProcessBatchAsync(stoppingToken);
                     }
                     catch (Exception ex) when (ex is not OperationCanceledException)
                     {
                            logger.LogWarning(ex, "Passage Outbox en échec — nouvel essai au prochain poll.");
                     }
                     await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
              }
       }

       private async Task ProcessBatchAsync(CancellationToken ct)
       {
              using var scope = scopeFactory.CreateScope();
              var db = scope.ServiceProvider.GetRequiredService<ShopPulseDbContext>();
              var orders = scope.ServiceProvider.GetRequiredService<EfOrderService>();
              var emails = scope.ServiceProvider.GetRequiredService<IOrderEmailSender>();

              var pending = await db.Outbox
                     .Where(m => m.ProcessedAt == null && m.Attempts < MaxAttempts)
                     .OrderBy(m => m.Id)
                     .Take(10)
                     .ToListAsync(ct);

              foreach (var message in pending)
              {
                     try
                     {
                            if (await orders.FindByIdAsync(message.OrderId, ct) is { } order)
                            {
                                   if (message.Type == OutboxTypes.OrderConfirmation)
                                          await emails.SendConfirmationAsync(order, ct);
                                   else
                                          await emails.SendStatusChangedAsync(order, ct);
                            }
                            message.ProcessedAt = DateTimeOffset.UtcNow;
                     }
                     catch (Exception ex)
                     {
                            message.Attempts++;
                            message.LastError = ex.Message[..Math.Min(ex.Message.Length, 1000)];
                            logger.LogWarning(ex,
                                   "Email Outbox {MessageId} ({Type}) : tentative {Attempts} en échec.",
                                   message.Id, message.Type, message.Attempts);
                     }
                     await db.SaveChangesAsync(ct);
              }
       }
}
