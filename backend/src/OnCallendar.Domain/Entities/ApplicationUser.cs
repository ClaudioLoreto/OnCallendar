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

    /// <summary>Codice fiscale del medico (per audit medico-legale).</summary>
    public string? FiscalCode { get; set; }
    /// <summary>Numero iscrizione albo medici.</summary>
    public string? MedicalRegistrationNumber { get; set; }

    /// <summary>Token push Expo per notifiche.</summary>
    public string? ExpoPushToken { get; set; }

    /// <summary>URL avatar (opzionale). Se null, l'app mostra le iniziali.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Numero di telefono dell'utente (per OTP/contatto).</summary>
    public string? Phone { get; set; }

    /// <summary>Preferenza lingua: "it" | "en".</summary>
    public string PreferredLanguage { get; set; } = "it";

    /// <summary>Preferenza tema: "light" | "dark" | "system".</summary>
    public string ThemePreference { get; set; } = "system";

    public bool IsActive { get; set; } = true;

    // Audit
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // Navigation
    public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
}
