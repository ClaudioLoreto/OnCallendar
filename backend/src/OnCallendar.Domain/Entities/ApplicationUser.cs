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

    /// <summary>
    /// Ruolo applicativo. Persistito in DB come stringa (Code) con FK
    /// alla tabella di lookup <c>RoleTypes</c>.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Medico;

    /// <summary>
    /// Numero progressivo del medico nel calendario storico (1..N).
    /// Permette di mappare il PDF (Medico 1/2/3/4) sull'utente reale.
    /// </summary>
    public int? MedicoNumber { get; set; }

    /// <summary>
    /// Codice badge breve (es. "M01", "M02"…) usato come alternativa
    /// all'email per il login rapido. Univoco a livello globale.
    /// </summary>
    public string? Badge { get; set; }

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

    /// <summary>
    /// Data UTC dell'ultimo cambio password (sia volontario che reset). Usato per
    /// determinare la scadenza annuale e forzare il cambio password al login.
    /// </summary>
    public DateTime? PasswordChangedAtUtc { get; set; }

    /// <summary>
    /// True se l'email è quella generata in fase di seed (es. medico1@navelli.local)
    /// e l'utente non l'ha ancora sostituita con la propria reale. Mostra una
    /// notifica fissa in app finché non viene confermata.
    /// </summary>
    public bool IsDefaultEmail { get; set; }

    /// <summary>
    /// Nuovo indirizzo email richiesto dall'utente, in attesa di conferma via link.
    /// Diventa <see cref="IdentityUser{Guid}.Email"/> solo dopo conferma.
    /// </summary>
    public string? PendingEmail { get; set; }

    /// <summary>Token monouso per confermare il cambio email.</summary>
    public string? EmailChangeToken { get; set; }

    /// <summary>Data UTC di invio della richiesta di cambio email (anti-replay).</summary>
    public DateTime? EmailChangeRequestedAtUtc { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
