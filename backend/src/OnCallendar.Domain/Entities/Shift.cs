using OnCallendar.Domain.Common;
using OnCallendar.Domain.Enums;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Singolo turno del calendario di guardia di Navelli.
/// Ogni record ha un Codice (F/FN/P/PN/N) un Medico di Turno (titolare)
/// e un Medico Reperibile (backup chiamato in caso di sovraccarico/assenza).
/// </summary>
public class Shift : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Data di inizio del turno (data del calendario, ora locale).</summary>
    public DateOnly Date { get; set; }

    public ShiftCode Code { get; set; }

    /// <summary>Inizio turno in UTC (calcolato a partire da Date + Code).</summary>
    public DateTime StartUtc { get; set; }
    /// <summary>Fine turno in UTC (esclusiva).</summary>
    public DateTime EndUtc { get; set; }

    /// <summary>Medico di turno titolare. Null se cessato/scoperto in attesa di assegnazione.</summary>
    public Guid? MedicoTurnoId { get; set; }
    public ApplicationUser? MedicoTurno { get; set; }

    /// <summary>Medico reperibile (backup). Null se non previsto.</summary>
    public Guid? MedicoReperibileId { get; set; }
    public ApplicationUser? MedicoReperibile { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.Assigned;

    public string? Notes { get; set; }

    public TimeSpan Duration => EndUtc - StartUtc;

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
