namespace OnCallendar.Infrastructure.Mail;

/// <summary>
/// Configurazione SMTP letta da appsettings (sezione "Mail").
///
/// Esempio per Gmail (richiede una "App Password" generata da
/// https://myaccount.google.com/apppasswords dopo aver attivato la 2FA):
///
///   "Mail": {
///     "Enabled": true,
///     "Host": "smtp.gmail.com",
///     "Port": 587,
///     "UseStartTls": true,
///     "User": "superboy23.mail@gmail.com",
///     "Password": "xxxx xxxx xxxx xxxx",
///     "Sender": "superboy23.mail@gmail.com",
///     "SenderName": "OnCallendar Navelli",
///     "SendAsInitiator": false
///   }
/// </summary>
public sealed class MailSettings
{
    /// <summary>Se false, IEmailSender è no-op (utile in CI/test).</summary>
    public bool Enabled { get; set; }

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;

    public string? User { get; set; }
    public string? Password { get; set; }

    /// <summary>Indirizzo "From" effettivo (deve coincidere con User per Gmail).</summary>
    public string? Sender { get; set; }
    public string? SenderName { get; set; }

    /// <summary>
    /// Se true (PROD), il MAIL FROM resta sempre il sender configurato
    /// (richiesto da Gmail/SMTP), ma il REPLY-TO viene impostato sulla
    /// mail dell'utente che ha fatto partire l'azione, in modo che le
    /// risposte arrivino direttamente a lui. In DEV lasciare false.
    /// </summary>
    public bool SendAsInitiator { get; set; }
}
