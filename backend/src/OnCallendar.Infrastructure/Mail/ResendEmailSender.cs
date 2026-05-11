using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnCallendar.Application.Common.Interfaces;

namespace OnCallendar.Infrastructure.Mail;

/// <summary>
/// IEmailSender basato su <a href="https://resend.com">Resend</a>: invio
/// via API HTTP REST con API key (no password SMTP in chiaro).
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    private const string ApiUrl = "https://api.resend.com/emails";

    private readonly MailSettings _settings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        IOptions<MailSettings> options,
        IHttpClientFactory httpFactory,
        ILogger<ResendEmailSender> logger)
    {
        _settings = options.Value;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public bool IsEnabled =>
        _settings.Enabled
        && !string.IsNullOrWhiteSpace(_settings.ApiKey)
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
                "[Mail/Resend][NOOP] To={To} Subject={Subject} (Mail:Enabled=false o ApiKey/Sender mancanti)",
                toEmail, subject);
            return;
        }

        // Sandbox: in dev o senza dominio verificato Resend rifiuta i destinatari
        // diversi dal proprietario dell'account. SandboxRecipient redirige tutto
        // verso un singolo indirizzo verificato. Il subject resta pulito;
        // il destinatario originale finisce in Reply-To così è visibile dal client.
        var actualTo = toEmail;
        var sandboxMode = !string.IsNullOrWhiteSpace(_settings.SandboxRecipient);
        if (sandboxMode)
        {
            actualTo = _settings.SandboxRecipient!;
        }

        try
        {
            var fromHeader = string.IsNullOrWhiteSpace(_settings.SenderName)
                ? _settings.Sender!
                : $"{_settings.SenderName} <{_settings.Sender}>";

            // In sandbox Resend rifiuta il formato "Nome <email>" e vuole solo
            // l'email pulita per validare contro l'account owner.
            var toHeader = sandboxMode || string.IsNullOrWhiteSpace(toName)
                ? actualTo
                : $"{toName} <{actualTo}>";

            var body = new Dictionary<string, object?>
            {
                ["from"]    = fromHeader,
                ["to"]      = new[] { toHeader },
                ["subject"] = subject,
                ["html"]    = htmlBody,
                ["text"]    = plainBody ?? StripHtml(htmlBody),
            };

            // In sandbox: header non-standard X-Original-To così riconosci il destinatario
            // reale anche con subject pulito.
            if (sandboxMode)
            {
                body["headers"] = new Dictionary<string, string>
                {
                    ["X-Original-To"] = toEmail,
                };
            }

            var replyTo = !string.IsNullOrWhiteSpace(replyToEmail)
                ? replyToEmail
                : _settings.ReplyTo;
            if (!string.IsNullOrWhiteSpace(replyTo))
            {
                body["reply_to"] = string.IsNullOrWhiteSpace(replyToName)
                    ? replyTo
                    : $"{replyToName} <{replyTo}>";
            }

            using var http = _httpFactory.CreateClient("resend");
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

            using var resp = await http.PostAsJsonAsync(ApiUrl, body, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "[Mail/Resend] HTTP {Status} inviando a {To}: {Error}",
                    (int)resp.StatusCode, actualTo, err);
                return;
            }

            // Resend risponde { id: "..." } in 200 OK.
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var id = doc.RootElement.TryGetProperty("id", out var p) ? p.GetString() : "?";
            _logger.LogInformation(
                "[Mail/Resend] Sent To={To} Subject={Subject} ResendId={Id}",
                actualTo, subject, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Mail/Resend] Errore invio To={To} Subject={Subject}", actualTo, subject);
        }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(noTags).Trim();
    }
}
