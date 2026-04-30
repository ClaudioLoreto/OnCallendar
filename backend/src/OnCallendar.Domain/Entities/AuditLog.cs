using OnCallendar.Domain.Common;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Audit medico-legale immutabile. Ogni operazione su Shift / SwapRequest
/// produce una riga qui. Mai modificare/eliminare.
/// </summary>
public class AuditLog : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Es: "Shift", "ShiftAssignment", "SwapRequest", "ApplicationUser".</summary>
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }

    /// <summary>Es: "Created", "Updated", "Deleted", "SwapApproved", "SwapBlocked".</summary>
    public string Action { get; set; } = null!;

    public Guid? PerformedByUserId { get; set; }
    public string? PerformedByUserName { get; set; }

    public DateTime PerformedAtUtc { get; set; }

    /// <summary>Snapshot stato precedente (JSON).</summary>
    public string? OldValuesJson { get; set; }
    /// <summary>Snapshot stato nuovo (JSON).</summary>
    public string? NewValuesJson { get; set; }

    public string? Notes { get; set; }
}
