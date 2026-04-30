using OnCallendar.Domain.Common;
using OnCallendar.Domain.Enums;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Slot di turno (es. 20:00 -> 08:00 di Guardia Medica).
/// L'assegnazione effettiva al medico è in <see cref="ShiftAssignment"/>
/// (separata per tracciare lo storico passaggi di mano).
/// </summary>
public class Shift : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Inizio turno in UTC.</summary>
    public DateTime StartUtc { get; set; }
    /// <summary>Fine turno in UTC (esclusiva).</summary>
    public DateTime EndUtc { get; set; }

    public string? Location { get; set; }
    public string? Notes { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.Assigned;

    /// <summary>Numero massimo di medici assegnabili allo stesso slot (default: 2).</summary>
    public int Capacity { get; set; } = 2;

    /// <summary>Durata calcolata.</summary>
    public TimeSpan Duration => EndUtc - StartUtc;

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // Navigation
    public ICollection<ShiftAssignment> Assignments { get; set; } = new List<ShiftAssignment>();

    /// <summary>Assegnazione attualmente attiva (helper non mappato).</summary>
    public ShiftAssignment? CurrentAssignment =>
        Assignments.FirstOrDefault(a => a.IsCurrent && !a.IsDeleted);
}
