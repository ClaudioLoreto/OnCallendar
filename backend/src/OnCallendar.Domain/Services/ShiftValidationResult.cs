namespace OnCallendar.Domain.Services;

/// <summary>
/// Risultato della validazione di uno scambio/cessione/presa-da-bacheca.
/// Contiene 0..N violazioni; la swap è approvabile solo se IsValid == true.
/// </summary>
public sealed class ShiftValidationResult
{
    public bool IsValid => Violations.Count == 0;
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

public sealed record ShiftRuleViolation(
    ShiftRuleCode Code,
    string Message,
    Guid? MedicoId = null,
    Guid? ShiftId = null);

public enum ShiftRuleCode
{
    MaxConsecutiveHoursExceeded = 1,    // > 12h consecutive
    MinRestPeriodViolated       = 2,    // < 11h riposo tra due turni
    OverlappingShifts           = 3,    // turni sovrapposti
    ShiftInThePast              = 4,
    SameMedico                  = 5,
    InvalidShiftWindow          = 6
}
