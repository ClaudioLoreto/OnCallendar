namespace OnCallendar.Application.Common.Interfaces;

/// <summary>
/// Richiesta di invio notifica multi-canale verso un singolo utente.
/// Costruita dai siti di chiamata (Controllers / Services) e passata a
/// <see cref="INotificationDispatcher"/> che applica le preferenze utente,
/// salva la notifica in-app e fan-out su Email/Push secondo configurazione.
/// </summary>
public sealed record NotificationRequest(
    Guid TenantId,
    Guid RecipientUserId,
    string Type,
    Guid? RelatedEntityId,
    /// <summary>Variabili per il template (es. "InitiatorName", "ShiftDate", "ShiftCode", "Message").</summary>
    IReadOnlyDictionary<string, string?> Data,
    /// <summary>Path relativo del deep-link nel client (es. "/swaps/{id}").</summary>
    string? DeepLinkPath = null
);

/// <summary>
/// Orchestratore unico per le notifiche: salva in-app, manda mail e push
/// applicando le preferenze utente per ogni canale.
/// MAI bloccante per il flusso utente (gli errori vengono solo loggati).
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Invia la notifica. Esecuzione "fire-and-forget" rispetto al flusso
    /// principale: persistenza della Notification in-app è atomica con la
    /// SaveChanges che il chiamante eseguirà subito dopo (o ha già eseguito);
    /// l'invio email/push avviene in background.
    /// </summary>
    Task DispatchAsync(NotificationRequest request, CancellationToken ct = default);

    /// <summary>Versione batch (più destinatari). Più efficiente di N call seriali.</summary>
    Task DispatchManyAsync(IEnumerable<NotificationRequest> requests, CancellationToken ct = default);
}
