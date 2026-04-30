using OnCallendar.Domain.Common;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Rappresenta una "Guardia Medica" locale (ASL/postazione).
/// Tutti i dati operativi sono isolati per TenantId.
/// </summary>
public class Tenant : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;          // identificativo URL-safe
    public string? FiscalCode { get; set; }
    public string? Address { get; set; }
    public string TimeZoneId { get; set; } = "Europe/Rome";
    public bool IsActive { get; set; } = true;

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // Navigation
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}
