using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using OnCallendar.Application.Common.Interfaces;

namespace OnCallendar.Infrastructure.Mail;

/// <summary>
/// IEmailSender basato su MailKit. Funziona con Gmail SMTP (porta 587 STARTTLS).
/// </summary>
public sealed class MailKitEmailSender : IEmailSender
{
    private readonly MailSettings _settings;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<MailSettings> options, ILogger<MailKitEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public bool IsEnabled =>
        _settings.Enabled
        && !string.IsNullOrWhiteSpace(_settings.Host)
        && !string.IsNullOrWhiteSpace(_settings.User)
        && !string.IsNullOrWhiteSpace(_settings.Password)
        && !string.IsNullOrWhiteSpace(_settings.Sender);

    public async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? plainBody = null,
        string? replyToEmail = null,
        string? replyToName = null,
        CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation(
                "[Mail][NOOP] To={To} Subject={Subject} (Mail:Enabled=false o credenziali mancanti)",
                toEmail, subject);
            return;
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_settings.SenderName ?? "OnCallendar", _settings.Sender));
            msg.To.Add(new MailboxAddress(toName ?? toEmail, toEmail));
            msg.Subject = subject;

            if (_settings.SendAsInitiator && !string.IsNullOrWhiteSpace(replyToEmail))
            {
                msg.ReplyTo.Add(new MailboxAddress(replyToName ?? replyToEmail, replyToEmail));
            }

            var builder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = plainBody ?? StripHtml(htmlBody),
            };
            msg.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            var socketOpt = _settings.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.SslOnConnect;

            await client.ConnectAsync(_settings.Host, _settings.Port, socketOpt, ct);
            await client.AuthenticateAsync(_settings.User, _settings.Password, ct);
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("[Mail] Sent To={To} Subject={Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // Mai bloccare il flusso utente per un errore mail.
            _logger.LogError(ex, "[Mail] Errore invio To={To} Subject={Subject}", toEmail, subject);
        }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(noTags).Trim();
    }
}
