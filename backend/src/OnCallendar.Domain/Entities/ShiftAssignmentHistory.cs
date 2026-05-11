using OnCallendar.Domain.Common;

namespace OnCallendar.Domain.Entities;

/// <summary>
/// Storico immutabile delle assegnazioni di un turno: ogni volta che cambia
/// il medico assegnato (per swap/cessione/riassegnazione manuale o creazione)
/// viene aggiunta una riga con l'assegnazione precedente e la nuova.
/// Serve per ricostruire "chi era originariamente di turno il giorno X"
/// indipendentemente dagli scambi successivi.
/// </summary>
public class ShiftAssignmentHistory : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Turno a cui si riferisce la riga di storico.</summary>
    public Guid ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;

    /// <summary>Medico precedente (null se prima assegnazione).</summary>
    public Guid? PreviousMedicoId { get; set; }

    /// <summary>Medico nuovo (null se il turno è stato sganciato).</summary>
    public Guid? NewMedicoId { get; set; }

    /// <summary>Codice motivo: "Created" | "Swap" | "Giveaway" | "Reassigned" | "External".</summary>
    public string Reason { get; set; } = "Reassigned";

    /// <summary>Eventuale richiesta di swap correlata (per audit).</summary>
    public Guid? SwapRequestId { get; set; }

    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
