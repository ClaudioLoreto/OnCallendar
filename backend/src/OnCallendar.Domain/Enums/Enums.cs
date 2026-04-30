namespace OnCallendar.Domain.Enums;

public enum UserRole
{
    SuperAdmin = 1,
    Medico = 2
}

/// <summary>
/// Codice del turno secondo la classificazione storica del calendario di Navelli.
/// Gli orari sono in ora locale Europe/Rome.
/// </summary>
public enum ShiftCode
{
    /// <summary>Festivo Diurno: 08:00 → 20:00 (domenica e festivi).</summary>
    F  = 1,
    /// <summary>Festivo Notte: 20:00 → 08:00 del giorno dopo.</summary>
    FN = 2,
    /// <summary>Prefestivo Diurno: 10:00 → 20:00 (sabati e prefestivi).</summary>
    P  = 3,
    /// <summary>Prefestivo Notte: 20:00 → 08:00 del giorno dopo.</summary>
    PN = 4,
    /// <summary>Infrasettimanale Notte: 20:00 → 08:00 del giorno dopo (lun-ven feriali).</summary>
    N  = 5
}

public enum ShiftStatus
{
    /// <summary>Turno assegnato (stato normale).</summary>
    Assigned = 1,
    /// <summary>Pubblicato sulla bacheca: cercasi sostituto / cessione.</summary>
    OnBoard = 2,
    /// <summary>Coperto/completato (passato).</summary>
    Completed = 3,
    /// <summary>Annullato (soft).</summary>
    Cancelled = 4
}

public enum SwapRequestType
{
    /// <summary>A cede il turno a B (one-way).</summary>
    Giveaway = 1,
    /// <summary>A scambia un proprio turno con uno di B (two-way).</summary>
    Swap = 2,
    /// <summary>B prende un turno dalla bacheca.</summary>
    PickFromBoard = 3
}

public enum SwapRequestStatus
{
    Pending = 1,
    AutoApproved = 2,
    Rejected = 3,
    Cancelled = 4,
    BlockedByRules = 5
}
