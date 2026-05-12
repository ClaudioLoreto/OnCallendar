using OnCallendar.Domain.Common;
using OnCallendar.Domain.Enums;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Controproposta nell'ambito di una trattativa: il destinatario di una
/// SwapRequest può rispondere con un turno diverso ("ti do questo invece").
/// L'iniziatore può accettare, rifiutare o controproporre a sua volta.
/// Ping-pong illimitato finché una delle due parti accetta o annulla.
/// </summary>
public class SwapCounterOffer : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid SwapRequestId { get; set; }
    public SwapRequest SwapRequest { get; set; } = null!;

    /// <summary>Chi propone questo "round".</summary>
    public Guid ProposedByMedicoId { get; set; }
    public ApplicationUser ProposedByMedico { get; set; } = null!;

    /// <summary>Turno offerto in cambio (al posto di quello iniziale).</summary>
    public Guid OfferedShiftId { get; set; }
    public Shift OfferedShift { get; set; } = null!;

    public string? Message { get; set; }

    /// <summary>Pending | Accepted | Rejected | Superseded.</summary>
    public CounterOfferStatus Status { get; set; } = CounterOfferStatus.Pending;

    public DateTime? ResolvedAtUtc { get; set; }
}
