using OnCallendar.Domain.Entities;

namespace OnCallendar.Domain.Services;

/// <inheritdoc cref="IShiftValidationService"/>
public sealed class ShiftValidationService : IShiftValidationService
{
    /// <summary>Massimo lavoro consecutivo (catena di turni back-to-back senza 11h di stacco).</summary>
    public static readonly TimeSpan MaxConsecutiveWork = TimeSpan.FromHours(12);

    /// <summary>Riposo minimo richiesto tra due turni distinti.</summary>
    public static readonly TimeSpan MinRestPeriod = TimeSpan.FromHours(11);

    public ShiftValidationResult ValidateGiveaway(
        Shift shift,
        Guid? fromMedicoId,
        Guid toMedicoId,
        IReadOnlyCollection<Shift> targetMedicoExistingShifts)
    {
        ArgumentNullException.ThrowIfNull(shift);
        ArgumentNullException.ThrowIfNull(targetMedicoExistingShifts);

        var violations = new List<ShiftRuleViolation>();

        if (shift.EndUtc <= shift.StartUtc)
            violations.Add(new(ShiftRuleCode.InvalidShiftWindow,
                "Il turno ha intervallo non valido (End <= Start).", null, shift.Id,
                ShiftRuleSeverity.Block));

        if (fromMedicoId.HasValue && fromMedicoId.Value == toMedicoId)
            violations.Add(new(ShiftRuleCode.SameMedico,
                "Il medico cedente e il ricevente coincidono.", toMedicoId, shift.Id,
                ShiftRuleSeverity.Block));

        // Simulo l'assegnazione: aggiungo lo shift alla lista del destinatario
        var simulated = targetMedicoExistingShifts
            .Where(s => s.Id != shift.Id)
            .Append(shift)
            .OrderBy(s => s.StartUtc)
            .ToList();

        violations.AddRange(CheckRules(toMedicoId, simulated));

        return violations.Count == 0
            ? ShiftValidationResult.Success()
            : ShiftValidationResult.Failure(violations);
    }

    public ShiftValidationResult ValidateSwap(
        Guid medicoAId, Shift shiftA, IReadOnlyCollection<Shift> medicoAOtherShifts,
        Guid medicoBId, Shift shiftB, IReadOnlyCollection<Shift> medicoBOtherShifts)
    {
        ArgumentNullException.ThrowIfNull(shiftA);
        ArgumentNullException.ThrowIfNull(shiftB);
        ArgumentNullException.ThrowIfNull(medicoAOtherShifts);
        ArgumentNullException.ThrowIfNull(medicoBOtherShifts);

        var violations = new List<ShiftRuleViolation>();

        if (medicoAId == medicoBId)
            violations.Add(new(ShiftRuleCode.SameMedico,
                "Medico A e Medico B coincidono: scambio non valido.",
                Severity: ShiftRuleSeverity.Block));

        // Post-swap: A perde shiftA e prende shiftB ; B perde shiftB e prende shiftA
        var aAfter = medicoAOtherShifts
            .Where(s => s.Id != shiftA.Id && s.Id != shiftB.Id)
            .Append(shiftB)
            .OrderBy(s => s.StartUtc)
            .ToList();

        var bAfter = medicoBOtherShifts
            .Where(s => s.Id != shiftA.Id && s.Id != shiftB.Id)
            .Append(shiftA)
            .OrderBy(s => s.StartUtc)
            .ToList();

        violations.AddRange(CheckRules(medicoAId, aAfter));
        violations.AddRange(CheckRules(medicoBId, bAfter));

        return violations.Count == 0
            ? ShiftValidationResult.Success()
            : ShiftValidationResult.Failure(violations);
    }

    // ---------------------------------------------------------------------
    // Core algorithm
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifica overlap, riposo minimo e durata della "catena" di turni
    /// adiacenti per un singolo medico. La lista DEVE essere ordinata per StartUtc.
    /// </summary>
    private static IEnumerable<ShiftRuleViolation> CheckRules(Guid medicoId, IReadOnlyList<Shift> ordered)
    {
        if (ordered.Count == 0)
            yield break;

        // 1) Singolo turno > 12h: Warning forzabile (la 12h è una soglia di tutela,
        //    non un divieto: l'utente può prendersi la responsabilità di farlo).
        foreach (var s in ordered)
        {
            if (s.EndUtc - s.StartUtc > MaxConsecutiveWork)
                yield return new(
                    ShiftRuleCode.MaxConsecutiveHoursExceeded,
                    $"Il turno dura {(s.EndUtc - s.StartUtc).TotalHours:F1}h, supera la soglia di tutela di {MaxConsecutiveWork.TotalHours}h consecutive.",
                    medicoId, s.Id, ShiftRuleSeverity.Warning);
        }

        // 2) Sliding window per overlap, rest e catena consecutiva
        var chainStart = ordered[0].StartUtc;
        var chainEnd   = ordered[0].EndUtc;

        for (int i = 1; i < ordered.Count; i++)
        {
            var prev = ordered[i - 1];
            var curr = ordered[i];

            // Overlap: BLOCK invalicabile (non puoi essere in due posti).
            if (curr.StartUtc < prev.EndUtc)
            {
                yield return new(
                    ShiftRuleCode.OverlappingShifts,
                    $"Sovrapposizione tra turni {prev.Id} e {curr.Id}.",
                    medicoId, curr.Id, ShiftRuleSeverity.Block);
                // estendo comunque la catena per non perdere altri controlli
                chainEnd = curr.EndUtc > chainEnd ? curr.EndUtc : chainEnd;
                continue;
            }

            var gap = curr.StartUtc - prev.EndUtc;

            if (gap < MinRestPeriod)
            {
                // Stacco insufficiente: Warning forzabile (la reperibilità è standby,
                // non lavoro effettivo, quindi il medico può valutare se prendersela).
                if (gap > TimeSpan.Zero)
                {
                    yield return new(
                        ShiftRuleCode.MinRestPeriodViolated,
                        $"Riposo di {gap.TotalHours:F1}h tra i turni: soglia minima consigliata {MinRestPeriod.TotalHours}h.",
                        medicoId, curr.Id, ShiftRuleSeverity.Warning);
                }

                chainEnd = curr.EndUtc;

                // Catena di lavoro > 12h: Warning forzabile.
                var chainWork = SumWorkInChain(ordered, chainStart, chainEnd);
                if (chainWork > MaxConsecutiveWork)
                {
                    yield return new(
                        ShiftRuleCode.MaxConsecutiveHoursExceeded,
                        $"Catena di {chainWork.TotalHours:F1}h di lavoro senza riposo adeguato (soglia consigliata {MaxConsecutiveWork.TotalHours}h).",
                        medicoId, curr.Id, ShiftRuleSeverity.Warning);
                }
            }
            else
            {
                // Stacco OK -> nuova catena
                chainStart = curr.StartUtc;
                chainEnd   = curr.EndUtc;
            }
        }
    }

    private static TimeSpan SumWorkInChain(IReadOnlyList<Shift> ordered, DateTime chainStart, DateTime chainEnd)
    {
        var total = TimeSpan.Zero;
        foreach (var s in ordered)
        {
            if (s.StartUtc >= chainStart && s.EndUtc <= chainEnd)
                total += s.EndUtc - s.StartUtc;
        }
        return total;
    }
}
