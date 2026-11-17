using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace TaskTune.API.Support;

public interface ITransactionalEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);
}

public sealed class SmtpTransactionalEmailSender(IConfiguration configuration) : ITransactionalEmailSender
{
    public async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:Smtp:Host"];
        var port = configuration.GetValue<int?>("Email:Smtp:Port") ?? 587;
        var from = configuration["Email:Smtp:From"];
        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        var enableSsl = configuration.GetValue<bool?>("Email:Smtp:EnableSsl") ?? true;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) ||
            port is < 1 or > 65535 || !enableSsl)
            throw new InvalidOperationException("A TLS-enabled SMTP host, port, and sender must be configured.");

        using var message = new MailMessage(from, recipient, subject, body)
        {
            IsBodyHtml = false
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };

        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password ?? string.Empty);

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
