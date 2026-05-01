using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Application.Common.Services;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Domain.Services;
using OnCallendar.Infrastructure.Persistence;
using System.Net;

namespace OnCallendar.Api.Controllers;

/// <summary>
/// Richieste di scambio/cessione/presa-da-bacheca del Medico di Turno.
/// Il Medico Reperibile non è oggetto di swap.
/// </summary>
[ApiController]
[Route("api/swaps")]
[Authorize]
public sealed class SwapRequestsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IShiftValidationService _rules;
    private readonly IAuditLogger _audit;
    private readonly IEmailSender _mail;

    public SwapRequestsController(
        ApplicationDbContext db, ICurrentUserService user,
        IShiftValidationService rules, IAuditLogger audit,
        IEmailSender mail)
    {
        _db = db; _user = user; _rules = rules; _audit = audit; _mail = mail;
    }

    public sealed record ShiftBriefDto(
        Guid Id, string Date, string Code,
        DateTime StartUtc, DateTime EndUtc);

    public sealed record SwapDto(
        Guid Id, SwapRequestType Type, SwapRequestStatus Status,
        Guid InitiatorId, string InitiatorName,
        ShiftBriefDto InitiatorShift,
        Guid? CounterpartId, string? CounterpartName,
        ShiftBriefDto? CounterpartShift,
        string? Message, string? ResolutionReason,
        DateTime CreatedAtUtc, DateTime? ResolvedAtUtc,
        int PendingCounterOffersCount);

    private static ShiftBriefDto Brief(Shift s) => new(
        s.Id, s.Date.ToString("yyyy-MM-dd"), s.Code.ToString(), s.StartUtc, s.EndUtc);

    private static SwapDto Map(SwapRequest r) => new(
        r.Id, r.Type, r.Status,
        r.InitiatorMedicoId, $"{r.InitiatorMedico.FirstName} {r.InitiatorMedico.LastName}",
        Brief(r.InitiatorShift),
        r.CounterpartMedicoId,
        r.CounterpartMedico is null ? null : $"{r.CounterpartMedico.FirstName} {r.CounterpartMedico.LastName}",
        r.CounterpartShift is null ? null : Brief(r.CounterpartShift),
        r.Message, r.ResolutionReason, r.CreatedAtUtc, r.ResolvedAtUtc,
        r.CounterOffers?.Count(o => o.Status == "Pending") ?? 0);

    private IQueryable<SwapRequest> LoadSwaps() => _db.SwapRequests
        .Include(r => r.InitiatorMedico)
        .Include(r => r.CounterpartMedico)
        .Include(r => r.InitiatorShift)
        .Include(r => r.CounterpartShift)
        .Include(r => r.CounterOffers);

    // ---------- LIST ----------
    [HttpGet("incoming")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> Incoming()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var list = await LoadSwaps()
            .Where(r => r.CounterpartMedicoId == uid && r.Status == SwapRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("outgoing")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> Outgoing()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var list = await LoadSwaps()
            .Where(r => r.InitiatorMedicoId == uid)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(100)
            .ToListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> History()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var list = await LoadSwaps()
            .Where(r => (r.InitiatorMedicoId == uid || r.CounterpartMedicoId == uid))
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(200)
            .ToListAsync();
        return Ok(list.Select(Map));
    }

    // ---------- CREATE ----------
    public sealed record CreateGiveawayRequest(Guid ShiftId, Guid ToMedicoId, string? Message);

    [HttpPost("giveaway")]
    public async Task<ActionResult<SwapDto>> CreateGiveaway([FromBody] CreateGiveawayRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == req.ShiftId);
        if (shift is null) return NotFound(new { error = "Turno inesistente." });
        if (shift.MedicoTurnoId != uid) return Forbid();
        if (shift.StartUtc <= DateTime.UtcNow)
            return BadRequest(new { error = "Turno già iniziato." });

        var to = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.ToMedicoId);
        if (to is null) return BadRequest(new { error = "Destinatario inesistente." });
        if (to.Id == uid) return BadRequest(new { error = "Non puoi cedere il turno a te stesso." });

        var alreadyPending = await _db.SwapRequests.AnyAsync(r =>
            r.InitiatorShiftId == shift.Id && r.Status == SwapRequestStatus.Pending);
        if (alreadyPending)
            return Conflict(new { error = "Esiste già una richiesta in sospeso per questo turno." });

        var swap = new SwapRequest
        {
            TenantId = shift.TenantId,
            Type = SwapRequestType.Giveaway,
            Status = SwapRequestStatus.Pending,
            InitiatorMedicoId = uid,
            InitiatorShiftId = shift.Id,
            CounterpartMedicoId = to.Id,
            Message = req.Message,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.SwapRequests.Add(swap);
        _audit.Log("SwapRequest", swap.Id, "GiveawayCreated", shift.TenantId,
            newValues: new { swap.InitiatorMedicoId, swap.CounterpartMedicoId, swap.InitiatorShiftId });

        // Notifica al destinatario
        var initiator = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid);
        _db.Notifications.Add(MakeNotif(shift.TenantId, to.Id, "SwapIncoming",
            $"{FullName(initiator)} ti ha proposto il turno {shift.Code} del {shift.Date:dd/MM/yyyy}", swap.Id));

        await _db.SaveChangesAsync();

        await SendSwapMailAsync(
            to: to,
            initiator: initiator,
            subject: $"[OnCallendar] Nuova richiesta turno da {FullName(initiator)}",
            body: $"<p>Ciao {to.FirstName},</p>" +
                  $"<p><b>{FullName(initiator)}</b> ti ha proposto di prendere il suo turno " +
                  $"<b>{shift.Code}</b> del <b>{shift.Date:dd/MM/yyyy}</b>.</p>" +
                  (string.IsNullOrWhiteSpace(req.Message) ? string.Empty : $"<p><i>Messaggio:</i> {WebUtility.HtmlEncode(req.Message)}</p>") +
                  "<p>Apri l'app OnCallendar per accettare o rifiutare.</p>");

        return Ok(await ReloadDto(swap.Id));
    }

    public sealed record CreateSwapRequest(Guid MyShiftId, Guid OtherShiftId, string? Message);

    // ---------- MULTI-DESTINATARIO GIVEAWAY ----------
    public sealed record CreateMultiGiveawayRequest(Guid ShiftId, List<Guid> RecipientIds, string? Message);

    /// <summary>
    /// Crea una cessione verso uno o più colleghi contemporaneamente.
    /// Il primo che accetta prende il turno; tutte le altre richieste pendenti
    /// per quel turno vengono cancellate automaticamente.
    /// </summary>
    [HttpPost("giveaway-multi")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> CreateMultiGiveaway([FromBody] CreateMultiGiveawayRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        if (req.RecipientIds is null || req.RecipientIds.Count == 0)
            return BadRequest(new { error = "Seleziona almeno un destinatario." });
        if (req.RecipientIds.Contains(uid))
            return BadRequest(new { error = "Non puoi cedere il turno a te stesso." });

        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == req.ShiftId);
        if (shift is null) return NotFound(new { error = "Turno inesistente." });
        if (shift.MedicoTurnoId != uid) return Forbid();
        if (shift.StartUtc <= DateTime.UtcNow)
            return BadRequest(new { error = "Turno già iniziato o passato." });

        // Annulla eventuali richieste pendenti precedenti per questo turno
        var existing = await _db.SwapRequests
            .Where(r => r.InitiatorShiftId == shift.Id && r.Status == SwapRequestStatus.Pending)
            .ToListAsync();
        foreach (var ex in existing)
        {
            ex.Status = SwapRequestStatus.Cancelled;
            ex.ResolvedAtUtc = DateTime.UtcNow;
            ex.ResolutionReason = "Rimpiazzato da nuova richiesta";
        }

        var recipients = await _db.Users
            .Where(u => req.RecipientIds.Contains(u.Id) && u.Id != uid)
            .ToListAsync();
        if (recipients.Count == 0)
            return BadRequest(new { error = "Nessun destinatario valido trovato." });

        var initiator = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid);
        var created = new List<SwapRequest>();

        foreach (var to in recipients)
        {
            var swap = new SwapRequest
            {
                TenantId = shift.TenantId,
                Type = SwapRequestType.Giveaway,
                Status = SwapRequestStatus.Pending,
                InitiatorMedicoId = uid,
                InitiatorShiftId = shift.Id,
                CounterpartMedicoId = to.Id,
                Message = req.Message,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.SwapRequests.Add(swap);
            _db.Notifications.Add(MakeNotif(shift.TenantId, to.Id, "SwapIncoming",
                $"{FullName(initiator)} ti ha proposto il turno {shift.Code} del {shift.Date:dd/MM/yyyy}", swap.Id));
            created.Add(swap);
        }

        _audit.Log("SwapRequest", Guid.NewGuid(), "MultiGiveawayCreated", shift.TenantId,
            newValues: new { ShiftId = shift.Id, RecipientCount = created.Count });
        await _db.SaveChangesAsync();

        // Mail asincrona a ogni destinatario
        foreach (var (swap, to) in created.Zip(recipients))
        {
            await SendSwapMailAsync(to, initiator,
                $"[OnCallendar] Nuova richiesta turno da {FullName(initiator)}",
                $"<p>Ciao {to.FirstName},</p>" +
                $"<p><b>{FullName(initiator)}</b> ti ha proposto di prendere il suo turno " +
                $"<b>{shift.Code}</b> del <b>{shift.Date:dd/MM/yyyy}</b>.</p>" +
                (string.IsNullOrWhiteSpace(req.Message) ? string.Empty
                    : $"<p><i>Messaggio:</i> {WebUtility.HtmlEncode(req.Message)}</p>") +
                "<p>Apri l'app OnCallendar per accettare o rifiutare.</p>");
        }

        var ids = created.Select(s => s.Id).ToList();
        var fresh = await LoadSwaps().Where(r => ids.Contains(r.Id)).ToListAsync();
        return Ok(fresh.Select(Map));
    }

    [HttpPost("swap")]
    public async Task<ActionResult<SwapDto>> CreateSwap([FromBody] CreateSwapRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var mine  = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == req.MyShiftId);
        var other = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == req.OtherShiftId);
        if (mine is null || other is null) return NotFound();
        if (mine.MedicoTurnoId != uid) return Forbid();
        if (other.MedicoTurnoId is null) return BadRequest(new { error = "Turno destinazione non assegnato." });
        if (other.MedicoTurnoId == uid) return BadRequest(new { error = "Non puoi scambiare con te stesso." });

        var alreadyPending = await _db.SwapRequests.AnyAsync(r =>
            (r.InitiatorShiftId == mine.Id || r.CounterpartShiftId == mine.Id ||
             r.InitiatorShiftId == other.Id || r.CounterpartShiftId == other.Id)
            && r.Status == SwapRequestStatus.Pending);
        if (alreadyPending)
            return Conflict(new { error = "Esiste già una richiesta in sospeso per uno dei due turni." });

        var swap = new SwapRequest
        {
            TenantId = mine.TenantId,
            Type = SwapRequestType.Swap,
            Status = SwapRequestStatus.Pending,
            InitiatorMedicoId = uid,
            InitiatorShiftId = mine.Id,
            CounterpartMedicoId = other.MedicoTurnoId,
            CounterpartShiftId = other.Id,
            Message = req.Message,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.SwapRequests.Add(swap);
        _audit.Log("SwapRequest", swap.Id, "SwapCreated", mine.TenantId,
            newValues: new { swap.InitiatorShiftId, swap.CounterpartShiftId });

        // Notifica alla controparte
        var swapInitiator = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid);
        _db.Notifications.Add(MakeNotif(mine.TenantId, other.MedicoTurnoId!.Value, "SwapIncoming",
            $"{FullName(swapInitiator)} ti ha proposto uno scambio turno del {mine.Date:dd/MM/yyyy}", swap.Id));

        await _db.SaveChangesAsync();
        return Ok(await ReloadDto(swap.Id));
    }

    // ---------- MULTI-DESTINATARIO SWAP ----------
    public sealed record CreateMultiSwapRequest(Guid MyShiftId, List<Guid> CandidateShiftIds, string? Message);

    /// <summary>
    /// Propone uno scambio del proprio turno verso più candidati contemporaneamente:
    /// per ogni turno candidato dei colleghi viene creata una SwapRequest separata;
    /// il primo che accetta vince e tutte le altre richieste pendenti per il proprio
    /// turno vengono auto-cancellate (gestito in ResolveAcceptance via cleanup).
    /// </summary>
    [HttpPost("swap-multi")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> CreateMultiSwap([FromBody] CreateMultiSwapRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        if (req.CandidateShiftIds is null || req.CandidateShiftIds.Count == 0)
            return BadRequest(new { error = "Seleziona almeno un turno candidato." });

        var mine = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == req.MyShiftId);
        if (mine is null) return NotFound(new { error = "Turno non trovato." });
        if (mine.MedicoTurnoId != uid) return Forbid();
        if (mine.StartUtc <= DateTime.UtcNow)
            return BadRequest(new { error = "Turno già iniziato o passato." });

        var candidateIds = req.CandidateShiftIds.Distinct().ToList();
        var candidates = await _db.Shifts
            .Where(s => candidateIds.Contains(s.Id) && s.MedicoTurnoId != null && s.MedicoTurnoId != uid)
            .ToListAsync();
        if (candidates.Count == 0)
            return BadRequest(new { error = "Nessun turno candidato valido." });

        // Annulla precedenti richieste pendenti che coinvolgono il mio turno
        var existing = await _db.SwapRequests
            .Where(r => (r.InitiatorShiftId == mine.Id || r.CounterpartShiftId == mine.Id)
                        && r.Status == SwapRequestStatus.Pending)
            .ToListAsync();
        foreach (var ex in existing)
        {
            ex.Status = SwapRequestStatus.Cancelled;
            ex.ResolvedAtUtc = DateTime.UtcNow;
            ex.ResolutionReason = "Rimpiazzato da nuova richiesta multi-scambio";
        }

        var initiator = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid);
        var created = new List<SwapRequest>();
        var notifiedRecipients = new List<(SwapRequest Swap, Domain.Entities.ApplicationUser To, Shift Cand)>();

        foreach (var cand in candidates)
        {
            var swap = new SwapRequest
            {
                TenantId = mine.TenantId,
                Type = SwapRequestType.Swap,
                Status = SwapRequestStatus.Pending,
                InitiatorMedicoId = uid,
                InitiatorShiftId = mine.Id,
                CounterpartMedicoId = cand.MedicoTurnoId!.Value,
                CounterpartShiftId = cand.Id,
                Message = req.Message,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.SwapRequests.Add(swap);
            var to = await _db.Users.FirstOrDefaultAsync(u => u.Id == cand.MedicoTurnoId!.Value);
            if (to != null)
            {
                _db.Notifications.Add(MakeNotif(mine.TenantId, to.Id, "SwapIncoming",
                    $"{FullName(initiator)} ti ha proposto uno scambio per il {mine.Date:dd/MM/yyyy}", swap.Id));
                notifiedRecipients.Add((swap, to, cand));
            }
            created.Add(swap);
        }

        _audit.Log("SwapRequest", Guid.NewGuid(), "MultiSwapCreated", mine.TenantId,
            newValues: new { MyShiftId = mine.Id, CandidateCount = created.Count });
        await _db.SaveChangesAsync();

        foreach (var (_, to, cand) in notifiedRecipients)
        {
            await SendSwapMailAsync(to, initiator,
                $"[OnCallendar] Proposta di scambio da {FullName(initiator)}",
                $"<p>Ciao {to.FirstName},</p>" +
                $"<p><b>{FullName(initiator)}</b> ti propone di scambiare il suo <b>{mine.Code}</b> del " +
                $"<b>{mine.Date:dd/MM/yyyy}</b> con il tuo <b>{cand.Code}</b> del <b>{cand.Date:dd/MM/yyyy}</b>.</p>" +
                (string.IsNullOrWhiteSpace(req.Message) ? string.Empty
                    : $"<p><i>Messaggio:</i> {WebUtility.HtmlEncode(req.Message)}</p>") +
                "<p>Apri l'app OnCallendar per accettare o rifiutare.</p>");
        }

        var ids = created.Select(s => s.Id).ToList();
        var fresh = await LoadSwaps().Where(r => ids.Contains(r.Id)).ToListAsync();
        return Ok(fresh.Select(Map));
    }

    [HttpPost("pick/{shiftId:guid}")]
    public async Task<ActionResult<SwapDto>> PickFromBoard(Guid shiftId)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift is null) return NotFound();
        if (shift.Status != ShiftStatus.OnBoard) return BadRequest(new { error = "Turno non in bacheca." });
        if (shift.MedicoTurnoId is null) return BadRequest(new { error = "Turno scoperto." });
        if (shift.MedicoTurnoId == uid) return BadRequest(new { error = "Sei già tu l'assegnatario." });

        var swap = new SwapRequest
        {
            TenantId = shift.TenantId,
            Type = SwapRequestType.PickFromBoard,
            Status = SwapRequestStatus.Pending,
            InitiatorMedicoId = shift.MedicoTurnoId.Value,
            InitiatorShiftId = shift.Id,
            CounterpartMedicoId = uid,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.SwapRequests.Add(swap);
        await _db.SaveChangesAsync();

        // Pick-from-board è un'azione del medico stesso: può forzare i Warning
        // implicitamente (qui passiamo force=true). I Block restano comunque tali.
        return await ResolveAcceptance(swap.Id, force: true);
    }

    // ---------- ACCEPT / REJECT / CANCEL ----------

    /// <summary>
    /// Accetta una richiesta. Se il rule engine torna SOLO Warning (es. catena di
    /// lavoro &gt; 12h, riposo &lt; 11h), il client può ripetere la chiamata con
    /// <c>?force=true</c> dopo aver confermato esplicitamente all'utente che si
    /// sta prendendo la responsabilità (la reperibilità è standby, non
    /// lavoro continuativo).
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    public Task<ActionResult<SwapDto>> Accept(Guid id, [FromQuery] bool force = false)
        => ResolveAcceptance(id, force);

    public sealed record RejectBody(string? Reason);

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<SwapDto>> Reject(Guid id, [FromBody] RejectBody? body)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == id);
        if (swap is null) return NotFound();
        if (swap.CounterpartMedicoId != uid) return Forbid();
        if (swap.Status != SwapRequestStatus.Pending)
            return BadRequest(new { error = "Richiesta non più pendente." });

        swap.Status = SwapRequestStatus.Rejected;
        swap.ResolvedAtUtc = DateTime.UtcNow;
        swap.ResolutionReason = body?.Reason;
        _audit.Log("SwapRequest", swap.Id, "Rejected", swap.TenantId, notes: body?.Reason);
        _db.Notifications.Add(MakeNotif(swap.TenantId, swap.InitiatorMedicoId, "SwapRejected",
            $"{FullName(swap.CounterpartMedico)} ha rifiutato il tuo turno del {swap.InitiatorShift.Date:dd/MM/yyyy}", swap.Id));
        await _db.SaveChangesAsync();

        await SendSwapMailAsync(
            to: swap.InitiatorMedico,
            initiator: swap.CounterpartMedico,
            subject: $"[OnCallendar] Richiesta rifiutata da {FullName(swap.CounterpartMedico)}",
            body: $"<p>Ciao {swap.InitiatorMedico.FirstName},</p>" +
                  $"<p><b>{FullName(swap.CounterpartMedico)}</b> ha rifiutato la tua richiesta sul turno " +
                  $"<b>{swap.InitiatorShift.Code}</b> del <b>{swap.InitiatorShift.Date:dd/MM/yyyy}</b>.</p>" +
                  (string.IsNullOrWhiteSpace(body?.Reason) ? string.Empty : $"<p><i>Motivo:</i> {WebUtility.HtmlEncode(body!.Reason!)}</p>"));

        return Ok(Map(swap));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SwapDto>> Cancel(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == id);
        if (swap is null) return NotFound();
        if (swap.InitiatorMedicoId != uid) return Forbid();
        if (swap.Status != SwapRequestStatus.Pending)
            return BadRequest(new { error = "Richiesta non più pendente." });

        swap.Status = SwapRequestStatus.Cancelled;
        swap.ResolvedAtUtc = DateTime.UtcNow;
        _audit.Log("SwapRequest", swap.Id, "Cancelled", swap.TenantId);
        if (swap.CounterpartMedicoId.HasValue)
            _db.Notifications.Add(MakeNotif(swap.TenantId, swap.CounterpartMedicoId.Value, "SwapCancelled",
                $"{FullName(swap.InitiatorMedico)} ha annullato la richiesta sul turno {swap.InitiatorShift.Date:dd/MM/yyyy}", swap.Id));
        await _db.SaveChangesAsync();

        if (swap.CounterpartMedico is not null)
        {
            await SendSwapMailAsync(
                to: swap.CounterpartMedico,
                initiator: swap.InitiatorMedico,
                subject: $"[OnCallendar] Richiesta annullata da {FullName(swap.InitiatorMedico)}",
                body: $"<p>Ciao {swap.CounterpartMedico.FirstName},</p>" +
                      $"<p><b>{FullName(swap.InitiatorMedico)}</b> ha annullato la richiesta sul turno " +
                      $"<b>{swap.InitiatorShift.Code}</b> del <b>{swap.InitiatorShift.Date:dd/MM/yyyy}</b>.</p>");
        }

        return Ok(Map(swap));
    }

    // ---------- CORE ----------
    private async Task<ActionResult<SwapDto>> ResolveAcceptance(Guid swapId, bool force = false, bool skipCallerAuthCheck = false)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return NotFound();
        if (!skipCallerAuthCheck && swap.CounterpartMedicoId != uid) return Forbid();
        if (swap.Status != SwapRequestStatus.Pending)
            return BadRequest(new { error = "Richiesta non più pendente." });

        var window = TimeSpan.FromDays(7);
        var initiatorId   = swap.InitiatorMedicoId;
        var counterpartId = swap.CounterpartMedicoId!.Value;

        var minStart = swap.InitiatorShift.StartUtc - window;
        var maxEnd   = swap.InitiatorShift.EndUtc + window;
        if (swap.CounterpartShift is not null)
        {
            if (swap.CounterpartShift.StartUtc - window < minStart) minStart = swap.CounterpartShift.StartUtc - window;
            if (swap.CounterpartShift.EndUtc + window > maxEnd)     maxEnd   = swap.CounterpartShift.EndUtc + window;
        }

        var counterpartShifts = await _db.Shifts
            .Where(s => s.MedicoTurnoId == counterpartId
                        && s.StartUtc < maxEnd && s.EndUtc > minStart
                        && s.Status != ShiftStatus.Cancelled)
            .ToListAsync();

        ShiftValidationResult result;

        if (swap.Type == SwapRequestType.Swap && swap.CounterpartShift is not null)
        {
            var initiatorShifts = await _db.Shifts
                .Where(s => s.MedicoTurnoId == initiatorId
                            && s.StartUtc < maxEnd && s.EndUtc > minStart
                            && s.Status != ShiftStatus.Cancelled)
                .ToListAsync();

            result = _rules.ValidateSwap(
                initiatorId,   swap.InitiatorShift,   initiatorShifts,
                counterpartId, swap.CounterpartShift, counterpartShifts);
        }
        else
        {
            result = _rules.ValidateGiveaway(
                swap.InitiatorShift, initiatorId, counterpartId, counterpartShifts);
        }

        if (!result.IsValid)
        {
            // Block invalicabile (overlap, stesso medico, turno passato…)
            if (result.HasBlockingViolations)
            {
                swap.Status = SwapRequestStatus.BlockedByRules;
                swap.ResolvedAtUtc = DateTime.UtcNow;
                swap.ResolutionReason = string.Join(" | ",
                    result.Violations.Select(v => $"[{v.Code}] {v.Message}"));
                _audit.Log("SwapRequest", swap.Id, "BlockedByRules", swap.TenantId,
                    notes: swap.ResolutionReason);
                await _db.SaveChangesAsync();

                return UnprocessableEntity(new
                {
                    error = "Scambio bloccato dal Rule Engine.",
                    canForce = false,
                    violations = result.Violations
                });
            }

            // Solo Warning: forzabile dall'utente con conferma esplicita.
            if (!force)
            {
                // NON cambio lo stato: la swap resta Pending, il client mostra
                // un alert e ripete la chiamata con ?force=true se l'utente conferma.
                return UnprocessableEntity(new
                {
                    error = "Conferma necessaria: superi una soglia di tutela.",
                    canForce = true,
                    violations = result.Violations
                });
            }

            // force=true: registro che è stato forzato e procedo.
            _audit.Log("SwapRequest", swap.Id, "AcceptedWithWarnings", swap.TenantId,
                notes: string.Join(" | ", result.Violations.Select(v => $"[{v.Code}] {v.Message}")));
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Reassign initiator shift → counterpart
        var oldInitiatorMedico = swap.InitiatorShift.MedicoTurnoId;
        swap.InitiatorShift.MedicoTurnoId = counterpartId;
        swap.InitiatorShift.UpdatedAtUtc = DateTime.UtcNow;
        if (swap.InitiatorShift.Status == ShiftStatus.OnBoard)
            swap.InitiatorShift.Status = ShiftStatus.Assigned;
        _audit.Log("Shift", swap.InitiatorShift.Id, "Reassigned", swap.TenantId,
            oldValues: new { MedicoTurnoId = oldInitiatorMedico },
            newValues: new { MedicoTurnoId = counterpartId, SwapId = swap.Id });

        if (swap.Type == SwapRequestType.Swap && swap.CounterpartShift is not null)
        {
            var oldCounterMedico = swap.CounterpartShift.MedicoTurnoId;
            swap.CounterpartShift.MedicoTurnoId = initiatorId;
            swap.CounterpartShift.UpdatedAtUtc = DateTime.UtcNow;
            _audit.Log("Shift", swap.CounterpartShift.Id, "Reassigned", swap.TenantId,
                oldValues: new { MedicoTurnoId = oldCounterMedico },
                newValues: new { MedicoTurnoId = initiatorId, SwapId = swap.Id });
        }

        swap.Status = SwapRequestStatus.AutoApproved;
        swap.ResolvedAtUtc = DateTime.UtcNow;
        _audit.Log("SwapRequest", swap.Id, "AutoApproved", swap.TenantId,
            newValues: new { swap.Type, swap.InitiatorShiftId, swap.CounterpartShiftId });

        // Cancel siblings (altri giveaway pendenti per lo stesso turno → multi-destinatario)
        var siblings = await _db.SwapRequests
            .Where(r => r.InitiatorShiftId == swap.InitiatorShiftId
                        && r.Status == SwapRequestStatus.Pending
                        && r.Id != swap.Id)
            .ToListAsync();
        foreach (var sib in siblings)
        {
            sib.Status = SwapRequestStatus.Cancelled;
            sib.ResolvedAtUtc = DateTime.UtcNow;
            sib.ResolutionReason = "Turno già accettato da altro medico";
            _audit.Log("SwapRequest", sib.Id, "AutoCancelled", swap.TenantId);
            if (sib.CounterpartMedicoId.HasValue)
                _db.Notifications.Add(MakeNotif(swap.TenantId, sib.CounterpartMedicoId.Value, "SwapAutoCancel",
                    $"Il turno del {swap.InitiatorShift.Date:dd/MM/yyyy} è stato accettato da qualcun altro", sib.Id));
        }

        // Notifica all'iniziatore: accettato
        _db.Notifications.Add(MakeNotif(swap.TenantId, swap.InitiatorMedicoId, "SwapAccepted",
            $"{FullName(swap.CounterpartMedico)} ha accettato il tuo turno del {swap.InitiatorShift.Date:dd/MM/yyyy}", swap.Id));

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // Mail all'iniziatore
        await SendSwapMailAsync(
            to: swap.InitiatorMedico,
            initiator: swap.CounterpartMedico,
            subject: $"[OnCallendar] Richiesta accettata da {FullName(swap.CounterpartMedico)}",
            body: $"<p>Ciao {swap.InitiatorMedico.FirstName},</p>" +
                  $"<p><b>{FullName(swap.CounterpartMedico)}</b> ha accettato la tua richiesta sul turno " +
                  $"<b>{swap.InitiatorShift.Code}</b> del <b>{swap.InitiatorShift.Date:dd/MM/yyyy}</b>.</p>" +
                  (swap.CounterpartShift is null
                      ? string.Empty
                      : $"<p>In cambio prenderai il suo turno <b>{swap.CounterpartShift.Code}</b> del <b>{swap.CounterpartShift.Date:dd/MM/yyyy}</b>.</p>"));

        return Ok(await ReloadDto(swap.Id));
    }

    // ---------- TRATTATIVE / CONTROPROPOSTE ----------

    public sealed record CounterOfferDto(
        Guid Id, Guid SwapRequestId,
        Guid ProposedById, string ProposedByName,
        ShiftBriefDto OfferedShift,
        string? Message, string Status,
        DateTime CreatedAtUtc, DateTime? ResolvedAtUtc);

    private CounterOfferDto MapOffer(SwapCounterOffer o) => new(
        o.Id, o.SwapRequestId,
        o.ProposedByMedicoId, $"{o.ProposedByMedico.FirstName} {o.ProposedByMedico.LastName}",
        Brief(o.OfferedShift), o.Message, o.Status, o.CreatedAtUtc, o.ResolvedAtUtc);

    [HttpGet("{id:guid}/counter-offers")]
    public async Task<ActionResult<IEnumerable<CounterOfferDto>>> ListCounterOffers(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var swap = await _db.SwapRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (swap is null) return NotFound();
        if (swap.InitiatorMedicoId != uid && swap.CounterpartMedicoId != uid) return Forbid();

        var offers = await _db.SwapCounterOffers
            .Include(o => o.ProposedByMedico)
            .Include(o => o.OfferedShift)
            .Where(o => o.SwapRequestId == id)
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync();
        return Ok(offers.Select(MapOffer));
    }

    public sealed record CreateCounterOfferRequest(Guid OfferedShiftId, string? Message);

    /// <summary>
    /// Propone un turno DIVERSO al posto di quello richiesto.
    /// Può essere chiamato sia dal counterpart (risposta) sia dall'initiator
    /// (controproposta alla controproposta) → ping-pong illimitato.
    /// </summary>
    [HttpPost("{id:guid}/counter")]
    public async Task<ActionResult<CounterOfferDto>> ProposeCounter(Guid id, [FromBody] CreateCounterOfferRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == id);
        if (swap is null) return NotFound();
        if (swap.InitiatorMedicoId != uid && swap.CounterpartMedicoId != uid) return Forbid();
        if (swap.Status != SwapRequestStatus.Pending)
            return BadRequest(new { error = "La richiesta non è più trattabile." });

        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == req.OfferedShiftId);
        if (shift is null) return NotFound(new { error = "Turno offerto inesistente." });
        if (shift.MedicoTurnoId != uid)
            return BadRequest(new { error = "Puoi offrire solo turni di cui sei medico di turno." });
        if (shift.StartUtc <= DateTime.UtcNow)
            return BadRequest(new { error = "Il turno offerto è già iniziato." });

        // Marca le controproposte precedenti come superate
        var previous = await _db.SwapCounterOffers
            .Where(o => o.SwapRequestId == id && o.Status == "Pending")
            .ToListAsync();
        foreach (var p in previous)
        {
            p.Status = "Superseded";
            p.ResolvedAtUtc = DateTime.UtcNow;
        }

        var offer = new SwapCounterOffer
        {
            TenantId = swap.TenantId,
            SwapRequestId = swap.Id,
            ProposedByMedicoId = uid,
            OfferedShiftId = shift.Id,
            Message = req.Message,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.SwapCounterOffers.Add(offer);
        _audit.Log("SwapCounterOffer", offer.Id, "Created", swap.TenantId,
            newValues: new { swap.Id, offer.OfferedShiftId, offer.ProposedByMedicoId });

        // Notifica all'altra parte
        var otherUserId = uid == swap.InitiatorMedicoId
            ? swap.CounterpartMedicoId
            : swap.InitiatorMedicoId;
        if (otherUserId.HasValue)
        {
            var meName = uid == swap.InitiatorMedicoId
                ? FullName(swap.InitiatorMedico)
                : FullName(swap.CounterpartMedico);
            _db.Notifications.Add(MakeNotif(swap.TenantId, otherUserId.Value, "SwapCounter",
                $"{meName} ti ha proposto in cambio il turno {shift.Code} del {shift.Date:dd/MM/yyyy}", swap.Id));
        }

        await _db.SaveChangesAsync();

        var fresh = await _db.SwapCounterOffers
            .Include(o => o.ProposedByMedico).Include(o => o.OfferedShift)
            .FirstAsync(o => o.Id == offer.Id);
        return Ok(MapOffer(fresh));
    }

    /// <summary>
    /// Accetta una controproposta: il SwapRequest diventa di tipo Swap a tutti
    /// gli effetti (con la nuova contropartita), e viene risolto subito.
    /// </summary>
    [HttpPost("{swapId:guid}/counter/{offerId:guid}/accept")]
    public async Task<ActionResult<SwapDto>> AcceptCounter(Guid swapId, Guid offerId, [FromQuery] bool force = false)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return NotFound();
        if (swap.Status != SwapRequestStatus.Pending)
            return BadRequest(new { error = "Richiesta non più pendente." });

        var offer = await _db.SwapCounterOffers
            .Include(o => o.OfferedShift).Include(o => o.ProposedByMedico)
            .FirstOrDefaultAsync(o => o.Id == offerId && o.SwapRequestId == swapId);
        if (offer is null) return NotFound();
        if (offer.Status != "Pending") return BadRequest(new { error = "Controproposta non più valida." });

        // Solo l'altra parte può accettare la controproposta
        if (offer.ProposedByMedicoId == uid)
            return BadRequest(new { error = "Devi attendere la risposta dell'altro medico." });
        if (uid != swap.InitiatorMedicoId && uid != swap.CounterpartMedicoId) return Forbid();

        // Trasformo lo swap in uno scambio "Swap" con la nuova contropartita
        swap.Type = SwapRequestType.Swap;
        swap.CounterpartShiftId = offer.OfferedShiftId;
        swap.CounterpartShift = offer.OfferedShift;
        // Il counterpart è chi ha offerto
        swap.CounterpartMedicoId = offer.ProposedByMedicoId;
        swap.CounterpartMedico = offer.ProposedByMedico;

        offer.Status = "Accepted";
        offer.ResolvedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // ResolveAcceptance ha un check di auth (caller deve essere counterpart);
        // qui invece l'accettante può essere ANCHE l'iniziatore (chi riceve la
        // controproposta dell'altra parte), quindi disabilitiamo quel check
        // (l'auth è già stata fatta sopra: solo initiator/counterpart non-proposer).
        return await ResolveAcceptance(swap.Id, force, skipCallerAuthCheck: true);
    }

    [HttpPost("{swapId:guid}/counter/{offerId:guid}/reject")]
    public async Task<IActionResult> RejectCounter(Guid swapId, Guid offerId)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var swap = await _db.SwapRequests.FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return NotFound();
        var offer = await _db.SwapCounterOffers.FirstOrDefaultAsync(o => o.Id == offerId && o.SwapRequestId == swapId);
        if (offer is null) return NotFound();
        if (offer.ProposedByMedicoId == uid)
            return BadRequest(new { error = "Non puoi rifiutare la tua stessa proposta." });
        if (uid != swap.InitiatorMedicoId && uid != swap.CounterpartMedicoId) return Forbid();
        if (offer.Status != "Pending") return BadRequest(new { error = "Controproposta non più valida." });

        offer.Status = "Rejected";
        offer.ResolvedAtUtc = DateTime.UtcNow;

        _db.Notifications.Add(MakeNotif(swap.TenantId, offer.ProposedByMedicoId, "SwapCounterRejected",
            "La tua controproposta è stata rifiutata", swap.Id));

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- MAIL HELPERS ----------
    private static string FullName(ApplicationUser? u) =>
        u is null ? "qualcuno" : $"{u.FirstName} {u.LastName}".Trim();

    private Notification MakeNotif(Guid tenantId, Guid userId, string type, string message, Guid? relId = null) =>
        new()
        {
            TenantId = tenantId, UserId = userId, Type = type, Message = message,
            RelatedEntityId = relId, CreatedAtUtc = DateTime.UtcNow, IsRead = false,
        };

    private async Task SendSwapMailAsync(
        ApplicationUser? to,
        ApplicationUser? initiator,
        string subject,
        string body)
    {
        if (to is null || string.IsNullOrWhiteSpace(to.Email)) return;
        try
        {
            await _mail.SendAsync(
                toEmail: to.Email!,
                toName: FullName(to),
                subject: subject,
                htmlBody: body,
                replyToEmail: initiator?.Email,
                replyToName: initiator is null ? null : FullName(initiator));
        }
        catch
        {
            // Mai bloccare la response per la mail.
        }
    }

    private async Task<SwapDto> ReloadDto(Guid id)
    {
        var fresh = await LoadSwaps().FirstAsync(r => r.Id == id);
        return Map(fresh);
    }
}
