namespace OnCallendar.Domain.Enums;

public enum UserRole
{
    SuperAdmin = 1,
    Medico = 2
}

public enum ShiftStatus
{
    /// <summary>Turno creato e assegnato a un medico (stato normale).</summary>
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
    /// <summary>Bloccato dal Rule Engine per violazione vincoli.</summary>
    BlockedByRules = 5
}
