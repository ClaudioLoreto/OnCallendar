namespace OnCallendar.Domain.Services;

/// <summary>
/// Risultato della validazione di uno scambio/cessione/presa-da-bacheca.
/// Le violazioni hanno una severità:
///  - Block   = la regola è invalicabile (overlap, turno nel passato, stesso medico…)
///  - Warning = la regola è una soglia di tutela (es. 12h consecutive, riposo 11h)
///              che l'utente può forzare consapevolmente con ?force=true
/// </summary>
public sealed class ShiftValidationResult
{
    public bool IsValid => Violations.Count == 0;

    /// <summary>True se ci sono solo Warning (nessun Block): l'azione è forzabile.</summary>
    public bool HasOnlyWarnings =>
        Violations.Count > 0 && Violations.All(v => v.Severity == ShiftRuleSeverity.Warning);

    public bool HasBlockingViolations =>
        Violations.Any(v => v.Severity == ShiftRuleSeverity.Block);

    public IReadOnlyList<ShiftRuleViolation> Violations { get; }

    private ShiftValidationResult(IReadOnlyList<ShiftRuleViolation> violations)
        => Violations = violations;

    public static ShiftValidationResult Success() =>
        new(Array.Empty<ShiftRuleViolation>());

    public static ShiftValidationResult Failure(params ShiftRuleViolation[] violations) =>
        new(violations);

    public static ShiftValidationResult Failure(IEnumerable<ShiftRuleViolation> violations) =>
        new(violations.ToList());
}

public enum ShiftRuleSeverity
{
    /// <summary>Soglia di tutela. L'utente può forzare con conferma esplicita.</summary>
    Warning = 1,
    /// <summary>Regola invalicabile. Non forzabile da nessuno.</summary>
    Block = 2,
}

public sealed record ShiftRuleViolation(
    ShiftRuleCode Code,
    string Message,
    Guid? MedicoId = null,
    Guid? ShiftId = null,
    ShiftRuleSeverity Severity = ShiftRuleSeverity.Block);

public enum ShiftRuleCode
{
    MaxConsecutiveHoursExceeded = 1,    // > 12h consecutive  -> Warning (forzabile)
    MinRestPeriodViolated       = 2,    // < 11h riposo       -> Warning (forzabile)
    OverlappingShifts           = 3,    // turni sovrapposti  -> Block
    ShiftInThePast              = 4,    //                    -> Block
    SameMedico                  = 5,    //                    -> Block
    InvalidShiftWindow          = 6     //                    -> Block
}
