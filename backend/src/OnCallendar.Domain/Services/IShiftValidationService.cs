using OnCallendar.Domain.Entities;

namespace OnCallendar.Domain.Services;

/// <summary>
/// Valida uno scambio/cessione/presa-da-bacheca contro i vincoli di legge:
///   - max 12h consecutive di lavoro
///   - min 11h di riposo tra due turni
///   - nessuna sovrapposizione
///
/// Il servizio è PURO: riceve il contesto già caricato (turni esistenti dei
/// medici coinvolti) per essere facilmente testabile e indipendente dall'EF.
/// </summary>
public interface IShiftValidationService
{
    /// <summary>
    /// Valida una cessione/pick-from-board: <paramref name="shift"/>
    /// passa da <paramref name="fromMedicoId"/> a <paramref name="toMedicoId"/>.
    /// </summary>
    /// <param name="shift">Turno da trasferire.</param>
    /// <param name="fromMedicoId">Medico che cede (null se pick-from-board pubblico).</param>
    /// <param name="toMedicoId">Medico che riceve.</param>
    /// <param name="targetMedicoExistingShifts">
    /// Turni già assegnati al destinatario nella finestra di interesse
    /// (almeno [shift.Start - 24h, shift.End + 24h]).
    /// NON deve includere il turno stesso.
    /// </param>
    ShiftValidationResult ValidateGiveaway(
        Shift shift,
        Guid? fromMedicoId,
        Guid toMedicoId,
        IReadOnlyCollection<Shift> targetMedicoExistingShifts);

    /// <summary>
    /// Valida uno scambio bilaterale: medico A cede shiftA e riceve shiftB; viceversa per B.
    /// </summary>
    ShiftValidationResult ValidateSwap(
        Guid medicoAId, Shift shiftA, IReadOnlyCollection<Shift> medicoAOtherShifts,
        Guid medicoBId, Shift shiftB, IReadOnlyCollection<Shift> medicoBOtherShifts);
}
