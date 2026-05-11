using OnCallendar.Domain.Common;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Medico ESTERNO non censito tra gli utenti dell'applicazione. Non ha
/// credenziali di accesso, esiste solo come "etichetta" di copertura per un
/// singolo turno. Viene salvato in modo persistente per essere riproposto
/// come suggerimento (autocomplete) quando un altro medico vuole cedere un
/// turno alla stessa persona in futuro.
/// </summary>
public class ExternalDoctor : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string FirstName { get; set; } = string.Empty;
    public string LastName  { get; set; } = string.Empty;

    /// <summary>Forma normalizzata "firstname lastname" tutto lower-case, usata per match unique.</summary>
    public string NormalizedKey { get; set; } = string.Empty;

    /// <summary>Telefono di contatto facoltativo (per uso futuro).</summary>
    public string? Phone { get; set; }

    /// <summary>Email di contatto facoltativa, usata per inviare l'invito a registrarsi nell'app.</summary>
    public string? Email { get; set; }

    /// <summary>Token monouso per completare la registrazione tramite deep-link.</summary>
    public string? InviteToken { get; set; }

    /// <summary>Data UTC dell'ultimo invio dell'email di invito.</summary>
    public DateTime? InviteSentAtUtc { get; set; }

    /// <summary>Data UTC in cui il medico esterno ha completato la registrazione.</summary>
    public DateTime? RegisteredAtUtc { get; set; }

    /// <summary>FK opzionale all'utente applicativo creato dopo la registrazione.</summary>
    public Guid? LinkedUserId { get; set; }

    /// <summary>Note libere facoltative.</summary>
    public string? Notes { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : $"{FirstName} {LastName}".Trim();

    public static string Normalize(string firstName, string lastName)
        => $"{(firstName ?? string.Empty).Trim().ToLowerInvariant()} {(lastName ?? string.Empty).Trim().ToLowerInvariant()}".Trim();
}
