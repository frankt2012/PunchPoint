using System.Net;
using System.Net.Mail;
using PunchPoint.Core.Models;
using PunchPoint.Core.Security;

namespace PunchPoint.Core.Email;

/// <summary>
/// Sends mail through the SMTP server configured in Settings. Kept deliberately simple:
/// one server, optional STARTTLS, optional credentials (Gmail needs an app password).
/// </summary>
public static class EmailService
{
    public static bool IsConfigured(AppSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.SmtpHost) &&
        !string.IsNullOrWhiteSpace(settings.SmtpFrom) &&
        !string.IsNullOrWhiteSpace(settings.ReportEmail);

    public static void Send(AppSettings settings, IEnumerable<string> recipients, string subject,
        string body, params (string Name, byte[] Data)[] attachments)
    {
        var to = recipients
            .Where(r => !string.IsNullOrWhiteSpace(r) && r.Contains('@'))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (to.Count == 0)
            throw new InvalidOperationException("There is no valid email address to send to.");
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
            throw new InvalidOperationException("No SMTP server is configured (Settings → Email reports).");
        if (string.IsNullOrWhiteSpace(settings.SmtpFrom))
            throw new InvalidOperationException("No 'from' address is configured (Settings → Email reports).");

        using var message = new MailMessage
        {
            From = new MailAddress(settings.SmtpFrom),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };

        foreach (string recipient in to)
            message.To.Add(recipient);

        foreach (var (name, data) in attachments)
        {
            var stream = new MemoryStream(data);
            message.Attachments.Add(new Attachment(stream, name, "application/pdf"));
        }

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.SmtpUseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30_000,
        };

        if (!string.IsNullOrWhiteSpace(settings.SmtpUser))
            client.Credentials = new NetworkCredential(settings.SmtpUser, SecretProtector.Unprotect(settings.SmtpPassword));

        client.Send(message);
    }
}
