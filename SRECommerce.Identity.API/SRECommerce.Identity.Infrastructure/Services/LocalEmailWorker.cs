using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Infrastructure;

public sealed class LocalEmailWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<LocalEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Mailpit:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            try
            {
                // One local dispatcher; stable Message-ID supports at-least-once delivery.
                var jobs = await db.Emails.Where(e => e.DeliveredAt == null && e.NextAttemptAt <= DateTimeOffset.UtcNow).OrderBy(e => e.CreatedAt).Take(10).ToListAsync(stoppingToken);
                var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("SRECommerce.Identity.Email.v1");
                foreach (var job in jobs)
                {
                    try
                    {
                        var host=configuration["Mailpit:Host"] ?? "127.0.0.1";
                        if (host is not ("127.0.0.1" or "localhost" or "mailpit" or "host.docker.internal"))
                            throw new InvalidOperationException("Only the local Mailpit capture adapter is configured.");
                        using var smtp = new SmtpClient(host, configuration.GetValue("Mailpit:Port", 1025));
                        using var message = new MailMessage("identity@srecommerce.local", job.Recipient, job.Subject, protector.Unprotect(job.Body));
                        message.Headers.Add("Message-ID", $"<{job.Id:N}@identity.srecommerce.local>");
                        await smtp.SendMailAsync(message, stoppingToken);
                        job.DeliveredAt = DateTimeOffset.UtcNow;
                    }
                    catch (Exception ex) when (ex is SmtpException or System.Security.Cryptography.CryptographicException)
                    {
                        job.Attempts++;
                        job.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(job.Attempts, 6))));
                        logger.LogWarning("Local email delivery deferred for job {JobId}; attempt {Attempt}", job.Id, job.Attempts);
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Local email dispatcher unavailable; durable jobs remain pending ({ErrorType})", ex.GetType().Name);
            }
        }
    }
}
