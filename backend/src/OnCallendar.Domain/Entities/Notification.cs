using OnCallendar.Domain.Common;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Notifica in-app per un utente (swap incoming, accepted, rejected, ecc.).
/// Non soft-deleteable: vengono mantenute come storico lato lettura.
/// </summary>
public class Notification : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Utente destinatario della notifica.</summary>
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Tipo evento (vedi <see cref="OnCallendar.Domain.Notifications.NotificationTypeCodes"/>).
    /// Es: "SwapRequested", "SwapAccepted", "SwapRejected", "ShiftReassigned",
    /// "ExternalDoctorAssigned", "ReminderShiftTomorrow"...
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>Titolo breve (mostrato in lista notifiche e push).</summary>
    public string? Title { get; set; }

    /// <summary>Testo leggibile dall'utente (già nella lingua corretta).</summary>
    public string Message { get; set; } = null!;

    /// <summary>
    /// Categoria visiva: "swap" | "shift" | "system" | "reminder".
    /// Usata dal client per scegliere icona/colore.
    /// </summary>
    public string? Category { get; set; }

    public bool IsRead { get; set; }

    /// <summary>Id dell'entità correlata (SwapRequest, Shift, ...) per deep-link.</summary>
    public Guid? RelatedEntityId { get; set; }

    /// <summary>JSON con metadata extra per il client (deep-link, payload push, ...).</summary>
    public string? DataJson { get; set; }
}
