namespace OnCallendar.Infrastructure.Mail;

/// <summary>
/// Configurazione mail (sezione "Mail" in appsettings).
///
/// Supporta due provider:
///   • <b>Resend</b>  (consigliato): API HTTP con API key revocabile.
///                    Setup: https://resend.com → API Keys → Create.
///   • <b>Smtp</b>    (legacy/fallback): SMTP classico via MailKit
///                    (es. Gmail App Password).
/// </summary>
public sealed class MailSettings
{
    /// <summary>Se false, l'email sender è no-op (utile in CI/test).</summary>
    public bool Enabled { get; set; }

    /// <summary>"Resend" (default) | "Smtp".</summary>
    public string Provider { get; set; } = "Resend";

    // ---- Comuni a tutti i provider ----

    /// <summary>Indirizzo "From" effettivo (es. noreply@dominio).</summary>
    public string? Sender { get; set; }
    public string? SenderName { get; set; }

    /// <summary>Indirizzo "Reply-To" globale (opzionale).</summary>
    public string? ReplyTo { get; set; }

    /// <summary>Base URL del web app per costruire i deep-link nelle mail.</summary>
    public string? WebAppBaseUrl { get; set; }

    /// <summary>
    /// Se valorizzato, ha PRIORITÀ su <see cref="WebAppBaseUrl"/> per i deep-link
    /// nelle mail. Usato quando il backend gira in DEV con Expo Go (lo script
    /// start-expo.ps1 lo imposta dinamicamente al tunnel cloudflared di Metro,
    /// es. <c>exp+https://xxx.trycloudflare.com/--</c>) o, in futuro, per il
    /// deep-link nativo dell'app installata da store (<c>oncallendar://</c>).
    /// In produzione (Railway senza app mobile pubblicata) lascialo null così
    /// si ricade su <see cref="WebAppBaseUrl"/> (web app).
    /// </summary>
    public string? MobileDeepLinkBaseUrl { get; set; }

    /// <summary>
    /// Se valorizzato, TUTTE le mail vengono dirottate verso questo
    /// indirizzo (utile in DEV o con Resend free tier senza dominio
    /// verificato, dove Resend consegna solo all'owner dell'account).
    /// L'oggetto viene preceduto con il destinatario reale.
    /// </summary>
    public string? SandboxRecipient { get; set; }

    // ---- Resend ----

    /// <summary>API key Resend (formato re_xxxxxxxxxxxx). NON pubblicare in chiaro.</summary>
    public string? ApiKey { get; set; }

    // ---- SMTP (provider="Smtp") ----

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? User { get; set; }
    public string? Password { get; set; }
    /// <summary>Solo SMTP: in PROD imposta REPLY-TO sull'utente iniziatore.</summary>
    public bool SendAsInitiator { get; set; }
}

/// <summary>Configurazione push Expo (sezione "Push").</summary>
public sealed class PushSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Token opzionale per autenticare le richieste verso Expo Push API
    /// (consigliato in produzione, gratuito da expo.dev → Access Tokens).
    /// In dev può restare vuoto.
    /// </summary>
    public string? ExpoAccessToken { get; set; }
}
