namespace OnCallendar.Application.Common.Interfaces;

/// <summary>
/// Astrazione per l'invio di email.
/// L'implementazione concreta (MailKit/SMTP) vive in Infrastructure.
/// </summary>
public interface IEmailSender
{
    /// <summary>True se la configurazione SMTP è attiva e completa.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Invia una email HTML. In dev, se SMTP è disabilitato, registra solo
    /// in log e non solleva eccezioni (no-op silenzioso).
    /// </summary>
    Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? plainBody = null,
        string? replyToEmail = null,
        string? replyToName = null,
        CancellationToken ct = default);
}
