namespace OnCallendar.Domain.Entities.Lookup;

/// <summary>
/// Catalogo dei ruoli applicativi.
/// Tabella di lookup con PK testuale (Code) per leggibilita` in DB.
/// </summary>
public class RoleType
{
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;
}

/// <summary>
/// Catalogo dei tipi di turno (codici storici Navelli).
/// Contiene anche gli orari standard in ora locale Europe/Rome,
/// usati per calcolare StartUtc / EndUtc dei singoli Shift.
/// </summary>
public class ShiftType
{
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;

    /// <summary>Ora di inizio (locale Europe/Rome), 0..23.</summary>
    public int StartHourLocal { get; set; }

    /// <summary>Ora di fine (locale Europe/Rome), 1..24. 24 = midnight successiva.</summary>
    public int EndHourLocal { get; set; }

    /// <summary>True se il turno scavalca la mezzanotte (fine il giorno dopo).</summary>
    public bool IsOvernight { get; set; }
}

/// <summary>Stati possibili di un turno.</summary>
public class ShiftStatusType
{
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;
}

/// <summary>Tipi di richiesta swap (Giveaway / Swap / PickFromBoard).</summary>
public class SwapRequestTypeLookup
{
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;
}

/// <summary>Stati di una SwapRequest.</summary>
public class SwapRequestStatusType
{
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;
}
