using Microsoft.AspNetCore.Identity;
using OnCallendar.Domain.Common;
using OnCallendar.Domain.Enums;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Utente applicativo. Estende IdentityUser&lt;Guid&gt;.
/// SuperAdmin ha TenantId = null. I Medici hanno SEMPRE un TenantId.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>, ISoftDeletable
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;

    /// <summary>Null per SuperAdmin globale.</summary>
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public UserRole Role { get; set; } = UserRole.Medico;

    /// <summary>
    /// Numero progressivo del medico nel calendario storico (1..N).
    /// Permette di mappare il PDF (Medico 1/2/3/4) sull'utente reale.
    /// </summary>
    public int? MedicoNumber { get; set; }

    public string? FiscalCode { get; set; }
    public string? MedicalRegistrationNumber { get; set; }

    public string? ExpoPushToken { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }

    public string PreferredLanguage { get; set; } = "it";
    public string ThemePreference { get; set; } = "system";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
