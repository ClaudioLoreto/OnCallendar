using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Infrastructure.Mail;

namespace OnCallendar.Infrastructure.Notifications;

/// <summary>
/// Sender push tramite <a href="https://docs.expo.dev/push-notifications/sending-notifications/">Expo Push API</a>.
/// Gratis e senza account Apple/Firebase finché si usa Expo Go o build via Expo.
/// Mai bloccante: errori solo loggati.
/// </summary>
public sealed class ExpoPushSender : IExpoPushSender
{
    private const string ApiUrl = "https://exp.host/--/api/v2/push/send";
    // Expo accetta max 100 messaggi per chiamata.
    private const int BatchSize = 100;

    private readonly PushSettings _settings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ExpoPushSender> _logger;

    public ExpoPushSender(
        IOptions<PushSettings> options,
        IHttpClientFactory httpFactory,
        ILogger<ExpoPushSender> logger)
    {
        _settings = options.Value;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public bool IsEnabled => _settings.Enabled;

    public async Task<IReadOnlyList<string>> SendAsync(
        IEnumerable<string> tokens,
        string title,
        string body,
        string? category,
        IReadOnlyDictionary<string, string?>? data,
        CancellationToken ct = default)
    {
        if (!IsEnabled) return Array.Empty<string>();

        var validTokens = tokens
            .Where(t => !string.IsNullOrWhiteSpace(t)
                        && (t.StartsWith("ExponentPushToken[", StringComparison.Ordinal)
                            || t.StartsWith("ExpoPushToken[", StringComparison.Ordinal)))
            .Distinct()
            .ToList();

        if (validTokens.Count == 0) return Array.Empty<string>();

        var deadTokens = new List<string>();

        try
        {
            using var http = _httpFactory.CreateClient("expo-push");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(_settings.ExpoAccessToken))
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.ExpoAccessToken);
            }

            for (var i = 0; i < validTokens.Count; i += BatchSize)
            {
                var batch = validTokens.Skip(i).Take(BatchSize).ToList();
                var payload = batch.Select(token => new Dictionary<string, object?>
                {
                    ["to"]       = token,
                    ["title"]    = title,
                    ["body"]     = body,
                    ["sound"]    = "default",
                    ["priority"] = "high",
                    ["channelId"] = category ?? "default",
                    ["data"]     = data ?? new Dictionary<string, string?>(),
                });

                using var resp = await http.PostAsJsonAsync(ApiUrl, payload, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogError("[Push/Expo] HTTP {Status}: {Error}", (int)resp.StatusCode, err);
                    continue;
                }

                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array)
                    continue;

                var idx = 0;
                foreach (var item in dataEl.EnumerateArray())
                {
                    if (idx >= batch.Count) break;
                    var token = batch[idx++];
                    if (item.TryGetProperty("status", out var status) && status.GetString() == "error")
                    {
                        var details = item.TryGetProperty("details", out var d) ? d : default;
                        var errCode = details.ValueKind == JsonValueKind.Object && details.TryGetProperty("error", out var e)
                            ? e.GetString() : null;
                        if (errCode is "DeviceNotRegistered" or "InvalidCredentials")
                        {
                            deadTokens.Add(token);
                            _logger.LogWarning("[Push/Expo] Token invalido ({Err}): {Token}", errCode, token);
                        }
                        else
                        {
                            _logger.LogWarning("[Push/Expo] Errore consegna ({Err}): {Token}", errCode, token);
                        }
                    }
                }
            }

            _logger.LogInformation("[Push/Expo] Sent {Count} push, {Dead} token disattivati", validTokens.Count, deadTokens.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Push/Expo] Errore invio push");
        }

        return deadTokens;
    }
}
