using OnCallendar.Domain.Common;
using OnCallendar.Domain.Enums;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Richiesta P2P di scambio/cessione/presa-da-bacheca.
/// Il flusso è automatico: appena entrambe le parti coinvolte e
/// le regole risultano soddisfatte, la richiesta passa ad AutoApproved
/// e le assegnazioni vengono ribaltate.
/// </summary>
public class SwapRequest : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public SwapRequestType Type { get; set; }
    public SwapRequestStatus Status { get; set; } = SwapRequestStatus.Pending;

    /// <summary>Medico che propone (cede / scambia / pubblica).</summary>
    public Guid InitiatorMedicoId { get; set; }
    public ApplicationUser InitiatorMedico { get; set; } = null!;

    /// <summary>Turno offerto dall'iniziatore.</summary>
    public Guid InitiatorShiftId { get; set; }
    public Shift InitiatorShift { get; set; } = null!;

    /// <summary>Medico controparte. Null se è un'offerta aperta su bacheca.</summary>
    public Guid? CounterpartMedicoId { get; set; }
    public ApplicationUser? CounterpartMedico { get; set; }

    /// <summary>Turno della controparte (solo per Type = Swap).</summary>
    public Guid? CounterpartShiftId { get; set; }
    public Shift? CounterpartShift { get; set; }

    public string? Message { get; set; }

    /// <summary>True se la richiesta riguarda il ruolo di reperibilità (non il turno).</summary>
    public bool IsReperibile { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }
    /// <summary>Motivo blocco / rigetto (es. messaggio dal Rule Engine).</summary>
    public string? ResolutionReason { get; set; }

    /// <summary>Trattative ping-pong associate.</summary>
    public ICollection<SwapCounterOffer> CounterOffers { get; set; } = new List<SwapCounterOffer>();

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
