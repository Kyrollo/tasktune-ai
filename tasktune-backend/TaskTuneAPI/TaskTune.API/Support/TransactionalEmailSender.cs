using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace TaskTune.API.Support;

public interface ITransactionalEmailSender
{
    Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default);

    Task SendAsync(
        string recipient,
        string subject,
        string body,
        IReadOnlyCollection<string>? ccRecipients,
        CancellationToken cancellationToken = default);
}

public sealed class SmtpTransactionalEmailSender(IConfiguration configuration) : ITransactionalEmailSender
{
    public Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default) =>
        SendAsync(recipient, subject, body, null, cancellationToken);

    public async Task SendAsync(
        string recipient,
        string subject,
        string body,
        IReadOnlyCollection<string>? ccRecipients,
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

        if (!MailAddress.TryCreate(recipient, out var validatedRecipient) ||
            !string.Equals(validatedRecipient.Address, recipient, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The delivery recipient address is invalid.");

        using var message = new MailMessage(from, recipient, subject, body)
        {
            IsBodyHtml = false
        };

        foreach (var cc in (ccRecipients ?? Array.Empty<string>())
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!MailAddress.TryCreate(cc, out var validatedCc) ||
                !string.Equals(validatedCc.Address, cc, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A delivery CC address is invalid.");
            if (!string.Equals(cc, recipient, StringComparison.OrdinalIgnoreCase))
                message.CC.Add(validatedCc);
        }

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
