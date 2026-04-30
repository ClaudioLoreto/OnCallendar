using OnCallendar.Domain.Common;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Storia delle assegnazioni di un turno. Quando un turno viene scambiato
/// l'assegnazione precedente viene marcata IsCurrent=false (mai eliminata)
/// e ne viene creata una nuova. Garantisce audit medico-legale.
/// </summary>
public class ShiftAssignment : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public Guid ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;

    public Guid MedicoId { get; set; }
    public ApplicationUser Medico { get; set; } = null!;

    /// <summary>True se è l'assegnazione attualmente valida per il turno.</summary>
    public bool IsCurrent { get; set; } = true;

    public DateTime AssignedAtUtc { get; set; }

    /// <summary>Riferimento alla swap che ha generato questa assegnazione (se esiste).</summary>
    public Guid? OriginatingSwapRequestId { get; set; }
    public SwapRequest? OriginatingSwapRequest { get; set; }

    // Soft delete (in realtà non si elimina mai: si imposta IsCurrent=false)
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
