using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using ShopPulse.Application;
using ShopPulse.Domain;
using System.Globalization;

namespace ShopPulse.Infrastructure;

/// <summary>Email de confirmation via SMTP (Mailpit en développement, vrai relais en
/// production — configuration Email:Smtp:Host/Port/From). Sans hôte configuré : simple
/// journalisation, jamais d'exception. Le texte est en français invariant pour l'instant
/// (emails localisés : évolution ultérieure, la locale du client n'est pas stockée).</summary>
public sealed class SmtpOrderEmailSender(IConfiguration config, ILogger<SmtpOrderEmailSender> logger)
       : IOrderEmailSender
{
       public async Task SendConfirmationAsync(Order order, CancellationToken ct = default)
       {
              var host = config["Email:Smtp:Host"];
              if (string.IsNullOrEmpty(host))
              {
                     logger.LogInformation(
                            "SMTP non configuré : email de confirmation de la commande {OrderId} non envoyé.",
                            order.Id);
                     return;
              }

              var reference = order.Id.ToString("N")[..8].ToUpperInvariant();
              var lines = new List<string>
              {
                     $"Bonjour {order.FullName},",
                     "",
                     $"Nous avons bien reçu votre commande {reference} du {order.CreatedAt:dd/MM/yyyy}.",
                     "",
              };
              lines.AddRange(order.Lines.Select(l => string.Create(CultureInfo.InvariantCulture,
                     $"  {l.Quantity} × {l.ProductName} — {l.LineTotal:0.00} {order.Currency}")));
              lines.AddRange(
              [
                     "",
                     string.Create(CultureInfo.InvariantCulture, $"Total : {order.Total:0.00} {order.Currency}"),
                     "",
                     $"Livraison : {order.Address}, {order.PostalCode}",
                     "",
                     "Merci de votre confiance — ShopPulse",
              ]);
              var body = string.Join(Environment.NewLine, lines);

              var message = new MimeMessage();
              message.From.Add(MailboxAddress.Parse(config["Email:Smtp:From"] ?? "boutique@shoppulse.local"));
              message.To.Add(MailboxAddress.Parse(order.Email));
              message.Subject = $"Confirmation de votre commande {reference} — ShopPulse";
              message.Body = new TextPart("plain") { Text = body };

              using var client = new SmtpClient();
              await client.ConnectAsync(host, config.GetValue("Email:Smtp:Port", 1025),
                     SecureSocketOptions.None, ct);   // relais interne en clair ; TLS si relais externe
              await client.SendAsync(message, ct);
              await client.DisconnectAsync(quit: true, ct);
              logger.LogInformation("Email de confirmation de la commande {OrderId} envoyé à {Email}.",
                     order.Id, order.Email);
       }

       public async Task SendStatusChangedAsync(Order order, CancellationToken ct = default)
       {
              var host = config["Email:Smtp:Host"];
              if (string.IsNullOrEmpty(host)) return;

              var reference = order.Id.ToString("N")[..8].ToUpperInvariant();
              var (title, sentence) = order.Status switch
              {
                     OrderStatus.Paid => ("Paiement confirmé", "Votre paiement a été confirmé : votre commande est en préparation."),
                     OrderStatus.Shipped => ("Commande expédiée", "Bonne nouvelle : votre commande est en route !"),
                     OrderStatus.Cancelled => ("Commande annulée", "Votre commande a été annulée. Contactez-nous pour toute question."),
                     _ => ("Mise à jour de commande", $"Le statut de votre commande est désormais : {order.Status}."),
              };

              var message = new MimeMessage();
              message.From.Add(MailboxAddress.Parse(config["Email:Smtp:From"] ?? "boutique@shoppulse.local"));
              message.To.Add(MailboxAddress.Parse(order.Email));
              message.Subject = $"{title} — commande {reference} — ShopPulse";
              message.Body = new TextPart("plain")
              {
                     Text = string.Join(Environment.NewLine,
                            $"Bonjour {order.FullName},", "",
                            sentence, "",
                            string.Create(CultureInfo.InvariantCulture,
                                   $"Total : {order.Total:0.00} {order.Currency}"), "",
                            "Merci de votre confiance — ShopPulse")
              };

              using var client = new SmtpClient();
              await client.ConnectAsync(host, config.GetValue("Email:Smtp:Port", 1025),
                     SecureSocketOptions.None, ct);
              await client.SendAsync(message, ct);
              await client.DisconnectAsync(quit: true, ct);
              logger.LogInformation("Email de statut ({Status}) de la commande {OrderId} envoyé à {Email}.",
                     order.Status, order.Id, order.Email);
       }
}
