using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Notifications;
using OnCallendar.Infrastructure.Mail;
using OnCallendar.Infrastructure.Persistence;

namespace OnCallendar.Infrastructure.Notifications;

/// <summary>
/// Orchestratore unico delle notifiche multi-canale.
/// Per ogni richiesta:
///   1) salva la riga in <c>Notifications</c> (in-app),
///   2) se il canale Email è attivo per questo utente+evento, manda mail
///      (template HTML responsive nella lingua dell'utente),
///   3) se il canale Push è attivo, manda push Expo a tutti i device
///      registrati dell'utente, disattivando i token non più validi.
///
/// Esegue in modo "best effort": gli errori di invio mail/push non bloccano
/// mai il flusso utente — vengono solo loggati.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationTemplateRenderer _renderer;
    private readonly IEmailSender _email;
    private readonly IExpoPushSender _push;
    private readonly MailSettings _mailSettings;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        ApplicationDbContext db,
        INotificationTemplateRenderer renderer,
        IEmailSender email,
        IExpoPushSender push,
        IOptions<MailSettings> mailSettings,
        ILogger<NotificationDispatcher> logger)
    {
        _db = db;
        _renderer = renderer;
        _email = email;
        _push = push;
        _mailSettings = mailSettings.Value;
        _logger = logger;
    }

    public Task DispatchAsync(NotificationRequest request, CancellationToken ct = default)
        => DispatchManyAsync(new[] { request }, ct);

    public async Task DispatchManyAsync(IEnumerable<NotificationRequest> requests, CancellationToken ct = default)
    {
        var list = requests.ToList();
        if (list.Count == 0) return;

        // Carica una volta gli utenti e le preferenze coinvolte.
        var userIds = list.Select(r => r.RecipientUserId).Distinct().ToList();
        var users = await _db.Users
            .IgnoreQueryFilters() // attraversiamo il dispatcher anche per altri tenant in futuro
            .Where(u => userIds.Contains(u.Id) && !u.IsDeleted)
            .ToDictionaryAsync(u => u.Id, ct);

        var prefs = await _db.NotificationPreferences
            .IgnoreQueryFilters()
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(ct);

        var devicesByUser = await _db.UserDeviceTokens
            .IgnoreQueryFilters()
            .Where(d => userIds.Contains(d.UserId) && d.IsActive)
            .GroupBy(d => d.UserId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x).ToList(), ct);

        // 1) PERSISTENZA in-app (atomico): facciamo un solo SaveChanges
        var inAppCreated = new List<(NotificationRequest Req, Notification N, ApplicationUser User, RenderedNotification R, string Locale)>();

        foreach (var req in list)
        {
            if (!users.TryGetValue(req.RecipientUserId, out var user))
            {
                _logger.LogWarning("[Notify] Utente {UserId} non trovato, skip", req.RecipientUserId);
                continue;
            }

            var locale = (user.PreferredLanguage ?? "it").Trim().ToLowerInvariant();
            if (locale != "en") locale = "it";

            var deepLinkUrl = BuildDeepLinkUrl(req.DeepLinkPath);
            var rendered = _renderer.Render(req.Type, locale, req.Data, deepLinkUrl);

            // In-app
            if (IsChannelEnabled(prefs, req.RecipientUserId, req.Type, NotificationChannels.InApp))
            {
                var notif = new Notification
                {
                    TenantId = req.TenantId,
                    UserId = req.RecipientUserId,
                    Type = req.Type,
                    Title = rendered.Title,
                    Message = rendered.ShortMessage,
                    Category = NotificationTypeCodes.CategoryOf(req.Type),
                    RelatedEntityId = req.RelatedEntityId,
                    DataJson = SerializeData(req.Data, req.DeepLinkPath),
                    CreatedAtUtc = DateTime.UtcNow,
                };
                _db.Notifications.Add(notif);
                inAppCreated.Add((req, notif, user, rendered, locale));
            }
            else
            {
                // Anche se in-app è off, manteniamo i dati render per email/push
                inAppCreated.Add((req, null!, user, rendered, locale));
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Notify] Errore salvataggio notifiche in-app");
            // Non blocchiamo: tentiamo comunque email/push.
        }

        // 2) EMAIL — fan-out
        foreach (var item in inAppCreated)
        {
            if (string.IsNullOrWhiteSpace(item.User.Email)) continue;
            if (!IsChannelEnabled(prefs, item.User.Id, item.Req.Type, NotificationChannels.Email)) continue;

            try
            {
                await _email.SendAsync(
                    toEmail: item.User.Email!,
                    toName: $"{item.User.FirstName} {item.User.LastName}".Trim(),
                    subject: item.R.EmailSubject,
                    htmlBody: item.R.EmailHtmlBody,
                    plainBody: item.R.EmailTextBody,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Notify] Errore invio email a {Email}", item.User.Email);
            }
        }

        // 3) PUSH — raggruppato per utente
        foreach (var item in inAppCreated)
        {
            if (!IsChannelEnabled(prefs, item.User.Id, item.Req.Type, NotificationChannels.Push)) continue;
            if (!devicesByUser.TryGetValue(item.User.Id, out var devices) || devices.Count == 0) continue;

            var tokens = devices.Select(d => d.Token).ToList();
            var data = new Dictionary<string, string?>
            {
                ["type"] = item.Req.Type,
                ["category"] = NotificationTypeCodes.CategoryOf(item.Req.Type),
                ["relatedEntityId"] = item.Req.RelatedEntityId?.ToString(),
                ["deepLink"] = item.Req.DeepLinkPath,
            };

            try
            {
                var deadTokens = await _push.SendAsync(
                    tokens: tokens,
                    title: item.R.PushTitle,
                    body: item.R.PushBody,
                    category: NotificationTypeCodes.CategoryOf(item.Req.Type),
                    data: data,
                    ct: ct);

                if (deadTokens.Count > 0)
                {
                    var toDisable = devices.Where(d => deadTokens.Contains(d.Token)).ToList();
                    foreach (var d in toDisable) d.IsActive = false;
                    await _db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Notify] Errore invio push a utente {UserId}", item.User.Id);
            }
        }
    }

    private string? BuildDeepLinkUrl(string? path)
    {
        // Priorità: MobileDeepLinkBaseUrl (dev/Expo o app installata) > WebAppBaseUrl (web app/Railway).
        var baseUrl = !string.IsNullOrWhiteSpace(_mailSettings.MobileDeepLinkBaseUrl)
            ? _mailSettings.MobileDeepLinkBaseUrl!.TrimEnd('/')
            : (_mailSettings.WebAppBaseUrl ?? string.Empty).TrimEnd('/');

        if (string.IsNullOrWhiteSpace(path)) return string.IsNullOrEmpty(baseUrl) ? null : baseUrl;
        var p = path.StartsWith('/') ? path : "/" + path;
        return string.IsNullOrEmpty(baseUrl) ? p : baseUrl + p;
    }

    private static bool IsChannelEnabled(
        IReadOnlyList<NotificationPreference> prefs,
        Guid userId,
        string type,
        string channel)
    {
        // Default attivo. Solo se esiste una riga esplicita Enabled=false → off.
        var p = prefs.FirstOrDefault(x =>
            x.UserId == userId &&
            string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Channel, channel, StringComparison.OrdinalIgnoreCase));
        return p == null || p.Enabled;
    }

    private static string? SerializeData(IReadOnlyDictionary<string, string?> data, string? deepLink)
    {
        if (data.Count == 0 && string.IsNullOrEmpty(deepLink)) return null;
        var dict = new Dictionary<string, string?>(data, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(deepLink)) dict["deepLink"] = deepLink;
        return JsonSerializer.Serialize(dict);
    }
}
