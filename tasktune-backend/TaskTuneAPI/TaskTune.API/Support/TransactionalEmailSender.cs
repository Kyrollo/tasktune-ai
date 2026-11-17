using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace TaskTune.API.Support;

public interface ITransactionalEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);

    Task SendCommunicationAsync(
        string recipient,
        IReadOnlyCollection<string> ccEmails,
        string subject,
        string body,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (ccEmails.Count > 0)
            throw new NotSupportedException("This email adapter does not support CC recipients.");
        return SendAsync(recipient, subject, body, cancellationToken);
    }
}

public sealed class SmtpTransactionalEmailSender(IConfiguration configuration) : ITransactionalEmailSender
{
    public Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default) =>
        SendCoreAsync(recipient, [], subject, body, null, cancellationToken);

    public Task SendCommunicationAsync(
        string recipient,
        IReadOnlyCollection<string> ccEmails,
        string subject,
        string body,
        string messageId,
        CancellationToken cancellationToken = default) =>
        SendCoreAsync(recipient, ccEmails, subject, body, messageId, cancellationToken);

    private async Task SendCoreAsync(
        string recipient,
        IReadOnlyCollection<string> ccEmails,
        string subject,
        string body,
        string? stableMessageId,
        CancellationToken cancellationToken)
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
        if (!MailAddress.TryCreate(recipient, out _))
            throw new InvalidOperationException("The communication recipient address is invalid.");
        if (ccEmails.Count > 25 || ccEmails.Any(email => !MailAddress.TryCreate(email, out _)))
            throw new InvalidOperationException("The communication CC list is invalid.");

        using var message = new MailMessage(from, recipient, subject, body) { IsBodyHtml = false };
        foreach (var cc in ccEmails.Distinct(StringComparer.OrdinalIgnoreCase))
            message.CC.Add(new MailAddress(cc));

        if (!string.IsNullOrWhiteSpace(stableMessageId))
        {
            var senderDomain = from.Split('@').LastOrDefault();
            if (string.IsNullOrWhiteSpace(senderDomain) || senderDomain.Contains(' ')) senderDomain = "tasktune.invalid";
            message.Headers.Add("Message-ID", $"<tasktune-outbox-{stableMessageId}@{senderDomain}>");
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
