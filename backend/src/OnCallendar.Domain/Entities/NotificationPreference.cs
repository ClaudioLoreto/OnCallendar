using OnCallendar.Domain.Common;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Preferenza di un utente per ricevere (o meno) un certo evento di notifica
/// su un certo canale (Email / InApp / Push).
/// La riga esiste SOLO se l'utente ha disattivato esplicitamente la notifica:
/// di default tutto attivo.
/// </summary>
public class NotificationPreference : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Codice evento (vedi <see cref="OnCallendar.Domain.Notifications.NotificationTypeCodes"/>).</summary>
    public string Type { get; set; } = null!;

    /// <summary>Codice canale: "Email" | "InApp" | "Push".</summary>
    public string Channel { get; set; } = null!;

    /// <summary>Se false l'utente ha disabilitato esplicitamente questa coppia tipo+canale.</summary>
    public bool Enabled { get; set; } = true;
}
