using OnCallendar.Domain.Common;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Token push (Expo) registrato per un dispositivo dell'utente.
/// Lo stesso utente può avere più dispositivi (telefono+tablet+web).
/// La colonna <see cref="ApplicationUser.ExpoPushToken"/> resta come "ultimo
/// token" per compatibilità, ma la sorgente di verità è questa tabella.
/// </summary>
public class UserDeviceToken : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Expo push token (formato "ExponentPushToken[xxxxxxxxxxxxxx]").</summary>
    public string Token { get; set; } = null!;

    /// <summary>"ios" | "android" | "web".</summary>
    public string Platform { get; set; } = "unknown";

    /// <summary>Modello dispositivo (opzionale, debug).</summary>
    public string? DeviceName { get; set; }

    /// <summary>Aggiornato ad ogni login / chiamata.</summary>
    public DateTime LastSeenUtc { get; set; }

    /// <summary>Disabilitato dopo errori "DeviceNotRegistered" da Expo.</summary>
    public bool IsActive { get; set; } = true;
}
