namespace OnCallendar.Domain.Notifications;

/// <summary>
/// Codici dei canali di notifica. Stringhe (non enum) per allinearsi al
/// pattern lookup-table del resto del modello e per essere persistiti come
/// preferenze utente.
/// </summary>
public static class NotificationChannels
{
    public const string Email = "Email";
    public const string InApp = "InApp";
    public const string Push  = "Push";

    public static readonly IReadOnlyList<string> All = new[] { Email, InApp, Push };
}

/// <summary>
/// Codici dei tipi-evento di notifica. Tutti i siti che producono notifiche
/// devono usare uno di questi codici (no stringhe magiche sparse).
/// </summary>
public static class NotificationTypeCodes
{
    // -------- SWAP / CESSIONE --------
    /// <summary>Ricevuta una richiesta di scambio o cessione.</summary>
    public const string SwapRequested = "SwapRequested";
    /// <summary>La mia richiesta è stata accettata dal collega.</summary>
    public const string SwapAccepted = "SwapAccepted";
    /// <summary>La mia richiesta è stata rifiutata dal collega.</summary>
    public const string SwapRejected = "SwapRejected";
    /// <summary>L'iniziatore ha annullato una richiesta diretta a me.</summary>
    public const string SwapCancelled = "SwapCancelled";
    /// <summary>Sistema ha auto-cancellato una mia richiesta (timeout / sostituita).</summary>
    public const string SwapAutoCancelled = "SwapAutoCancelled";
    /// <summary>Ho ricevuto una controproposta su una mia richiesta in scambio.</summary>
    public const string CounterOfferReceived = "CounterOfferReceived";
    /// <summary>La mia controproposta è stata accettata.</summary>
    public const string CounterOfferAccepted = "CounterOfferAccepted";
    /// <summary>La mia controproposta è stata rifiutata.</summary>
    public const string CounterOfferRejected = "CounterOfferRejected";

    // -------- SHIFT / TURNI --------
    /// <summary>Mi è stato assegnato un nuovo turno (o riassegnato).</summary>
    public const string ShiftAssigned = "ShiftAssigned";
    /// <summary>Un mio turno è stato spostato/cambiato dall'admin.</summary>
    public const string ShiftReassigned = "ShiftReassigned";
    /// <summary>Un mio turno è stato cancellato.</summary>
    public const string ShiftRemoved = "ShiftRemoved";
    /// <summary>Un mio turno è stato pubblicato in bacheca/cessione.</summary>
    public const string ShiftPostedToBoard = "ShiftPostedToBoard";

    // -------- MEDICI ESTERNI --------
    /// <summary>L'admin ha affidato un mio turno a un medico esterno.</summary>
    public const string ExternalDoctorAssigned = "ExternalDoctorAssigned";

    // -------- REMINDER (cron) --------
    /// <summary>Promemoria: domani sei di turno.</summary>
    public const string ReminderShiftTomorrow = "ReminderShiftTomorrow";
    /// <summary>Promemoria: oggi sei di reperibilità.</summary>
    public const string ReminderOnCallToday = "ReminderOnCallToday";

    // -------- SISTEMA --------
    /// <summary>Notifica generica di sistema (manutenzioni, aggiornamenti).</summary>
    public const string SystemAnnouncement = "SystemAnnouncement";

    /// <summary>Tutti i tipi gestiti, in ordine di importanza per la UI Impostazioni.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        SwapRequested, SwapAccepted, SwapRejected, SwapCancelled,
        CounterOfferReceived, CounterOfferAccepted, CounterOfferRejected,
        ShiftAssigned, ShiftReassigned, ShiftRemoved, ShiftPostedToBoard,
        ExternalDoctorAssigned,
        ReminderShiftTomorrow, ReminderOnCallToday,
        SystemAnnouncement,
        SwapAutoCancelled,
    };

    /// <summary>Categoria UI per icone/colori del client.</summary>
    public static string CategoryOf(string type) => type switch
    {
        SwapRequested or SwapAccepted or SwapRejected or SwapCancelled
            or SwapAutoCancelled or CounterOfferReceived
            or CounterOfferAccepted or CounterOfferRejected => "swap",
        ShiftAssigned or ShiftReassigned or ShiftRemoved
            or ShiftPostedToBoard or ExternalDoctorAssigned => "shift",
        ReminderShiftTomorrow or ReminderOnCallToday => "reminder",
        _ => "system",
    };
}
