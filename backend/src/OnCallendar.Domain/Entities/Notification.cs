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
    /// Tipo: "SwapIncoming" | "SwapAccepted" | "SwapRejected" | "SwapCancelled" | "SwapAutoCancel".
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>Testo leggibile dall'utente (già in italiano).</summary>
    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    /// <summary>Id della SwapRequest correlata (opzionale per navigazione diretta).</summary>
    public Guid? RelatedEntityId { get; set; }
}
