using System.Net;

namespace OnCallendar.Infrastructure.Notifications;

/// <summary>
/// Layout HTML "stupendo e accattivante" delle email transazionali.
/// Stile inline (compatibile con tutti i client mail incluso Gmail/Outlook),
/// responsive su mobile, palette in linea con l'app (primary navy + accent
/// celeste, bg cream).
/// </summary>
internal static class EmailLayout
{
    private const string ColorPrimary    = "#355872";
    private const string ColorPrimaryDk  = "#243C50";
    private const string ColorAccent     = "#9CD5FF";
    private const string ColorBg         = "#F7F8F0";
    private const string ColorSurface    = "#FFFFFF";
    private const string ColorBorder     = "#E2E6E0";
    private const string ColorText       = "#243C50";
    private const string ColorMuted      = "#6B7B88";

    public static string Wrap(
        string title,
        string preheader,
        string bodyHtml,
        string? ctaLabel,
        string? ctaUrl,
        string footerHint,
        string locale)
    {
        var year = DateTime.UtcNow.Year;
        var encodedTitle = WebUtility.HtmlEncode(title);
        var encodedPreheader = WebUtility.HtmlEncode(preheader);

        var ctaHtml = string.IsNullOrWhiteSpace(ctaUrl) || string.IsNullOrWhiteSpace(ctaLabel)
            ? string.Empty
            : $@"
        <table role=""presentation"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""margin:28px 0 8px 0;"">
          <tr>
            <td bgcolor=""{ColorPrimary}"" style=""border-radius:10px;"">
              <a href=""{WebNetEncode(ctaUrl)}"" target=""_blank"" style=""display:inline-block;padding:14px 28px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:10px;letter-spacing:0.2px;"">
                {WebNetEncode(ctaLabel!)} &nbsp;→
              </a>
            </td>
          </tr>
        </table>";

        return $@"<!doctype html>
<html lang=""{locale}"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<meta name=""color-scheme"" content=""light"">
<meta name=""supported-color-schemes"" content=""light"">
<title>{encodedTitle}</title>
<style>
  @media (max-width:600px) {{
    .container {{ width:100% !important; }}
    .px {{ padding-left:20px !important; padding-right:20px !important; }}
    .hero h1 {{ font-size:22px !important; }}
  }}
</style>
</head>
<body style=""margin:0;padding:0;background:{ColorBg};font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif;color:{ColorText};-webkit-font-smoothing:antialiased;"">

<!-- Preheader (testo anteprima nascosto in client mail) -->
<div style=""display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;color:{ColorBg};"">
  {encodedPreheader}
</div>

<table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" bgcolor=""{ColorBg}"">
  <tr><td align=""center"" style=""padding:32px 12px;"">
    <table class=""container"" role=""presentation"" width=""600"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""width:600px;max-width:600px;"">

      <!-- HEADER brand -->
      <tr><td class=""px"" style=""padding:0 36px 0 36px;"">
        <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"">
          <tr>
            <td style=""padding:8px 0 14px 0;font-size:13px;color:{ColorMuted};letter-spacing:1.2px;text-transform:uppercase;font-weight:600;"">
              <span style=""display:inline-block;width:10px;height:10px;border-radius:50%;background:{ColorPrimary};vertical-align:middle;margin-right:8px;""></span>
              OnCallendar &middot; Navelli
            </td>
          </tr>
        </table>
      </td></tr>

      <!-- CARD bianca -->
      <tr><td>
        <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" bgcolor=""{ColorSurface}"" style=""background:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;overflow:hidden;"">

          <!-- Banda superiore primary con titolo -->
          <tr>
            <td class=""hero px"" style=""background:linear-gradient(135deg,{ColorPrimary} 0%,{ColorPrimaryDk} 100%);padding:32px 36px;color:#ffffff;"">
              <h1 style=""margin:0;font-size:26px;line-height:1.25;font-weight:700;color:#ffffff;letter-spacing:-0.2px;"">{encodedTitle}</h1>
              <div style=""margin-top:8px;height:3px;width:48px;background:{ColorAccent};border-radius:2px;""></div>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td class=""px"" style=""padding:28px 36px 8px 36px;font-size:15px;line-height:1.55;color:{ColorText};"">
              {bodyHtml}
              {ctaHtml}
            </td>
          </tr>

          <!-- Divider + footer hint -->
          <tr>
            <td class=""px"" style=""padding:8px 36px 28px 36px;"">
              <hr style=""border:none;border-top:1px solid {ColorBorder};margin:18px 0 14px 0;"">
              <p style=""margin:0;font-size:12px;line-height:1.5;color:{ColorMuted};"">
                {WebNetEncode(footerHint)}
              </p>
            </td>
          </tr>
        </table>
      </td></tr>

      <!-- Footer pagina -->
      <tr><td class=""px"" style=""padding:24px 36px;text-align:center;"">
        <p style=""margin:0;font-size:11px;color:{ColorMuted};line-height:1.6;"">
          © {year} OnCallendar — Guardia Medica Navelli<br>
          Questa è una notifica automatica, non rispondere a questa email.
        </p>
      </td></tr>

    </table>
  </td></tr>
</table>

</body>
</html>";
    }

    private static string WebNetEncode(string s) => WebUtility.HtmlEncode(s);
}
