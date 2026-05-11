namespace OnCallendar.Application.Common.Interfaces;

/// <summary>Output del template per un dato evento + locale.</summary>
public sealed record RenderedNotification(
    string Title,
    string ShortMessage,
    string EmailSubject,
    string EmailHtmlBody,
    string EmailTextBody,
    string PushTitle,
    string PushBody
);

/// <summary>
/// Genera il contenuto delle notifiche (titolo / corpo / mail HTML / push)
/// per un dato tipo evento e lingua, sostituendo le variabili dal payload.
/// </summary>
public interface INotificationTemplateRenderer
{
    /// <summary>Renderizza il template per <paramref name="type"/> in <paramref name="locale"/>.</summary>
    /// <param name="type">Codice evento (vedi <c>NotificationTypeCodes</c>).</param>
    /// <param name="locale">"it" | "en". Fallback su "it" se non presente.</param>
    /// <param name="data">Variabili (es. InitiatorName, ShiftDate, ShiftCode, ...).</param>
    /// <param name="deepLinkUrl">URL completo per il bottone CTA (può essere null).</param>
    RenderedNotification Render(
        string type,
        string locale,
        IReadOnlyDictionary<string, string?> data,
        string? deepLinkUrl);
}

/// <summary>Sender push per Expo (sicuro: errori loggati, mai bloccanti).</summary>
public interface IExpoPushSender
{
    bool IsEnabled { get; }

    /// <summary>
    /// Invia una notifica push a uno o più Expo push token.
    /// Token "DeviceNotRegistered" vengono restituiti per essere disattivati a DB.
    /// </summary>
    Task<IReadOnlyList<string>> SendAsync(
        IEnumerable<string> tokens,
        string title,
        string body,
        string? category,
        IReadOnlyDictionary<string, string?>? data,
        CancellationToken ct = default);
}
