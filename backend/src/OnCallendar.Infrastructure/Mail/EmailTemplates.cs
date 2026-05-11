using System.Web;

namespace OnCallendar.Infrastructure.Mail;

/// <summary>
/// Template HTML brandizzati per le email transazionali di OnCallendar.
/// Stile coerente con l'app mobile: palette #2563eb, layout card con header,
/// pulsante CTA prominente, footer di sicurezza.
/// </summary>
public static class EmailTemplates
{
    // Palette brand OnCallendar — stessa di EmailLayout (notifiche) e dell'app mobile.
    private const string Primary = "#355872";
    private const string PrimaryDark = "#243C50";
    private const string Accent = "#9CD5FF";
    private const string Bg = "#F7F8F0";
    private const string Card = "#FFFFFF";
    private const string Text = "#243C50";
    private const string Muted = "#6B7B88";
    private const string Border = "#E2E6E0";

    /// <summary>
    /// Costruisce un'email "branded" con header, paragrafi, CTA opzionale e footer.
    /// </summary>
    /// <param name="preheader">Anteprima nascosta (mostrata nelle anteprime client).</param>
    /// <param name="title">Titolo grande in alto.</param>
    /// <param name="greetingName">Nome destinatario per il saluto (puo` essere null).</param>
    /// <param name="paragraphs">Paragrafi del corpo (HTML safe-encoded a monte oppure semplici stringhe).</param>
    /// <param name="ctaLabel">Etichetta del bottone CTA (null = nessun bottone).</param>
    /// <param name="ctaUrl">URL del bottone CTA (null = nessun bottone).</param>
    /// <param name="footerNote">Nota piccola in fondo (es. "Se non hai richiesto tu...").</param>
    public static string Build(
        string preheader,
        string title,
        string? greetingName,
        IEnumerable<string> paragraphs,
        string? ctaLabel = null,
        string? ctaUrl = null,
        string? footerNote = null)
    {
        var paragraphsHtml = string.Join("\n",
            paragraphs.Select(p => $"<p style=\"margin:0 0 14px 0;color:{Text};font-size:15px;line-height:1.55;\">{p}</p>"));

        var greeting = !string.IsNullOrWhiteSpace(greetingName)
            ? $"<p style=\"margin:0 0 14px 0;color:{Text};font-size:15px;line-height:1.55;\">Ciao <b>{HttpUtility.HtmlEncode(greetingName)}</b>,</p>"
            : string.Empty;

        var ctaHtml = (!string.IsNullOrWhiteSpace(ctaLabel) && !string.IsNullOrWhiteSpace(ctaUrl))
            ? $@"
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:18px 0;"">
                <tr>
                  <td align=""center"" bgcolor=""{Primary}"" style=""border-radius:8px;"">
                    <a href=""{ctaUrl}"" target=""_blank"" style=""display:inline-block;padding:14px 28px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:8px;"">{HttpUtility.HtmlEncode(ctaLabel)}</a>
                  </td>
                </tr>
              </table>
              <p style=""margin:0 0 14px 0;color:{Muted};font-size:12px;line-height:1.55;"">Se il bottone non funziona, copia e incolla questo link nel browser:<br/><a href=""{ctaUrl}"" style=""color:{Primary};word-break:break-all;"">{ctaUrl}</a></p>"
            : string.Empty;

        var footerHtml = !string.IsNullOrWhiteSpace(footerNote)
            ? $"<p style=\"margin:18px 0 0 0;color:{Muted};font-size:12px;line-height:1.5;border-top:1px solid {Border};padding-top:14px;\">{footerNote}</p>"
            : string.Empty;

        return $@"<!DOCTYPE html>
<html lang=""it"">
<head>
<meta charset=""UTF-8"" />
<meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
<title>{HttpUtility.HtmlEncode(title)}</title>
</head>
<body style=""margin:0;padding:0;background:{Bg};font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Oxygen,Ubuntu,sans-serif;"">
  <span style=""display:none !important;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;"">{HttpUtility.HtmlEncode(preheader)}</span>
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background:{Bg};"">
    <tr>
      <td align=""center"" style=""padding:24px 12px;"">
        <table role=""presentation"" width=""560"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width:560px;width:100%;background:{Card};border-radius:14px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.08);"">
          <!-- Header -->
          <tr>
            <td style=""background:linear-gradient(135deg,{Primary},{PrimaryDark});padding:22px 28px;"">
              <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                  <td style=""color:#ffffff;font-size:20px;font-weight:800;letter-spacing:-0.3px;"">
                    OnCallendar
                  </td>
                  <td align=""right"" style=""color:rgba(255,255,255,0.8);font-size:12px;"">Notifica account</td>
                </tr>
              </table>
            </td>
          </tr>
          <!-- Body -->
          <tr>
            <td style=""padding:28px;"">
              <h1 style=""margin:0 0 16px 0;color:{Text};font-size:22px;line-height:1.3;font-weight:700;"">{HttpUtility.HtmlEncode(title)}</h1>
              {greeting}
              {paragraphsHtml}
              {ctaHtml}
              {footerHtml}
            </td>
          </tr>
          <!-- Footer -->
          <tr>
            <td style=""background:{Bg};padding:14px 28px;text-align:center;color:{Muted};font-size:11px;"">
              &copy; OnCallendar &middot; Gestione turni di guardia medica<br/>
              Questo messaggio &egrave; stato inviato automaticamente, non rispondere.
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    /// <summary>
    /// Risolve il base URL del callback per i link di conferma/reset nelle email.
    /// Priorita`:
    /// 1. <paramref name="clientCallbackUrl"/> fornito dal client (validato contro allowlist)
    /// 2. MailSettings.MobileDeepLinkBaseUrl
    /// 3. MailSettings.WebAppBaseUrl
    /// </summary>
    public static string ResolveCallbackBaseUrl(string? clientCallbackUrl, MailSettings mail)
    {
        if (!string.IsNullOrWhiteSpace(clientCallbackUrl))
        {
            var url = clientCallbackUrl.Trim().TrimEnd('/');
            // Allowlist degli scheme accettati: web (https), app standalone (oncallendar),
            // Expo Go in dev (exp/exps), e localhost http per sviluppo desktop.
            var lower = url.ToLowerInvariant();
            if (lower.StartsWith("https://") ||
                lower.StartsWith("oncallendar://") ||
                lower.StartsWith("exp://") ||
                lower.StartsWith("exps://") ||
                lower.StartsWith("http://localhost") ||
                lower.StartsWith("http://127.0.0.1"))
            {
                return url;
            }
        }
        if (!string.IsNullOrWhiteSpace(mail.MobileDeepLinkBaseUrl))
            return mail.MobileDeepLinkBaseUrl!.TrimEnd('/');
        return (mail.WebAppBaseUrl ?? string.Empty).TrimEnd('/');
    }
}
