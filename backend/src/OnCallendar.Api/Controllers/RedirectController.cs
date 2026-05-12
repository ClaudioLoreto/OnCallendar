using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Web;

namespace OnCallendar.Api.Controllers;

/// <summary>
/// Endpoint pubblico che riceve una destinazione (deep-link app, https web app
/// o exp:// di Expo Go) e restituisce una pagina HTML brandizzata che prova ad
/// aprirla automaticamente. Serve a fare in modo che i link nelle email siano
/// sempre <c>https://</c> (cliccabili in qualsiasi client di posta), anche
/// quando puntano in realta` ad uno scheme custom non cliccabile direttamente
/// dal client mail (exp://, oncallendar://, ...).
///
/// Esempio: l'email contiene
/// <code>https://api.example.com/r/go?to=exp%3A%2F%2F...%2F--%2Fconfirm-email%3Ftoken%3DXYZ</code>
/// Il browser apre la pagina, che immediatamente fa
/// <c>window.location = decodedTo</c>. Se l'app non risponde, mostra un bottone
/// di fallback e link agli store.
/// </summary>
[ApiController]
[Route("r")]
[AllowAnonymous]
public sealed class RedirectController : ControllerBase
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https", "oncallendar", "exp", "exps",
    };

    [HttpGet("go")]
    [Produces("text/html")]
    public IActionResult Go([FromQuery] string? to)
    {
        if (string.IsNullOrWhiteSpace(to))
            return BadRequest("Destinazione mancante.");

        // Validazione anti-open-redirect: solo scheme della nostra allowlist.
        if (!Uri.TryCreate(to, UriKind.Absolute, out var uri) ||
            !AllowedSchemes.Contains(uri.Scheme))
        {
            return BadRequest("Destinazione non valida.");
        }

        // Encode JS-safe della destinazione (single-quote, backslash, newline).
        var jsEncoded = HttpUtility.JavaScriptStringEncode(to, addDoubleQuotes: false);
        var htmlEncoded = HttpUtility.HtmlEncode(to);

        // Pagina brand OnCallendar con auto-redirect.
        // Palette: stesso schema usato negli email templates.
        var sb = new StringBuilder();
        sb.Append(@"<!doctype html>
<html lang=""it""><head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Apertura OnCallendar...</title>
<style>
  :root { color-scheme: light; }
  body { margin:0; background:#F7F8F0; font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif; color:#243C50; }
  .wrap { min-height:100vh; display:flex; align-items:center; justify-content:center; padding:24px; }
  .card { background:#FFF; border-radius:16px; box-shadow:0 6px 24px rgba(36,60,80,.12); max-width:440px; width:100%; padding:32px 28px; text-align:center; }
  h1 { color:#355872; font-size:22px; margin:0 0 12px; }
  p { color:#243C50; font-size:15px; line-height:1.5; margin:8px 0; }
  .muted { color:#6B7B88; font-size:13px; }
  .btn { display:inline-block; margin-top:18px; background:#355872; color:#FFF !important; text-decoration:none; padding:14px 28px; border-radius:10px; font-weight:600; font-size:15px; }
  .btn:hover { background:#243C50; }
  .stores { margin-top:18px; }
  .stores a { color:#355872; text-decoration:none; margin:0 8px; font-size:13px; }
  .spinner { width:32px; height:32px; border:3px solid #9CD5FF; border-top-color:#355872; border-radius:50%; animation:spin 1s linear infinite; margin:0 auto 16px; }
  @keyframes spin { to { transform:rotate(360deg); } }
</style>
</head>
<body>
  <div class=""wrap"">
    <div class=""card"">
      <div class=""spinner""></div>
      <h1>Apertura OnCallendar...</h1>
      <p>Stai per essere reindirizzato all'app.</p>
      <p class=""muted"">Se non si apre automaticamente, tocca il bottone qui sotto.</p>
      <a id=""open"" class=""btn"" href=""");
        sb.Append(htmlEncoded);
        sb.Append(@""">Apri OnCallendar</a>
      <p class=""stores muted"">
        Non hai l'app? <a href=""https://apps.apple.com/"">App Store</a> &middot; <a href=""https://play.google.com/store"">Google Play</a>
      </p>
    </div>
  </div>
<script>
  // Auto-redirect immediato.
  (function(){
    try { window.location.href = '");
        sb.Append(jsEncoded);
        sb.Append(@"'; } catch(e) {}
  })();
</script>
</body></html>");

        return Content(sb.ToString(), "text/html; charset=utf-8");
    }
}
