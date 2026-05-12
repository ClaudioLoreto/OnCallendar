using OnCallendar.Domain.Enums;

namespace OnCallendar.Domain.Services;

/// <summary>
/// Utility condivise per orari turni e generazione token.
/// </summary>
public static class ShiftTimeHelper
{
    /// <summary>Calcola gli orari locali (Europe/Rome) di un turno in base al codice.</summary>
    public static (DateTime startLocal, DateTime endLocal) ComputeLocalWindow(DateOnly date, ShiftCode code)
    {
        var d = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return code switch
        {
            ShiftCode.F  => (d.AddHours(8),  d.AddHours(20)),
            ShiftCode.FN => (d.AddHours(20), d.AddDays(1).AddHours(8)),
            ShiftCode.P  => (d.AddHours(10), d.AddHours(20)),
            ShiftCode.PN => (d.AddHours(20), d.AddDays(1).AddHours(8)),
            ShiftCode.N  => (d.AddHours(20), d.AddDays(1).AddHours(8)),
            _ => throw new ArgumentOutOfRangeException(nameof(code))
        };
    }

    public static DateOnly? TryParseDate(string? s)
        => DateOnly.TryParse(s, out var d) ? d : null;
}
