using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SRECommerce.Notification.Application;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Infrastructure;

public sealed class DevelopmentNotificationTransport(IConfiguration settings, IHostEnvironment environment) : INotificationTransport
{
    private string Host => settings["Notifications:MailpitHost"] ?? "127.0.0.1";
    public bool Available => environment.IsDevelopment() && settings.GetValue<bool>("Notifications:DeliveryEnabled") &&
        Host is "localhost" or "127.0.0.1" or "mailpit";

    public async Task<DeliveryResult> Send(NotificationJob job, CancellationToken ct)
    {
        if (!Available) return new(DeliveryState.Pending, "TransportDisabled", true);
        if (job.Channel is NotificationChannel.Sms or NotificationChannel.WhatsApp)
            return new(DeliveryState.Sent, "MockDelivery");
        if (!MailAddress.TryCreate(job.Recipient, out var recipient) || recipient.Address != job.Recipient)
            return new(DeliveryState.Failed, "InvalidRecipient");
        using var message = new MailMessage(new MailAddress("store@local.example"), recipient) { Subject = job.Subject, Body = job.Body, IsBodyHtml = false };
        message.Headers.Add("Message-ID", $"<{job.EventId:N}@srecommerce.local>");
        using var client = new SmtpClient(Host, 1025) { DeliveryMethod = SmtpDeliveryMethod.Network, UseDefaultCredentials = false };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try { await client.SendMailAsync(message, deadline.Token); return new(DeliveryState.Sent, "MailpitAccepted"); }
        catch (SmtpFailedRecipientException ex) when ((int)ex.StatusCode is >= 400 and < 500)
        { return new(DeliveryState.Pending, "MailpitTemporaryRejection", true); }
        catch (SmtpFailedRecipientException ex) when ((int)ex.StatusCode >= 500)
        { return new(DeliveryState.Failed, "MailpitRejectedRecipient"); }
        catch (SmtpException ex) when (ex.InnerException is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.ConnectionRefused })
        { return new(DeliveryState.Pending, "MailpitUnavailable", true); }
        catch (Exception ex) when (ex is SmtpException or OperationCanceledException)
        { return new(DeliveryState.NeedsReview, "DeliveryOutcomeUnknown"); }
    }
}
