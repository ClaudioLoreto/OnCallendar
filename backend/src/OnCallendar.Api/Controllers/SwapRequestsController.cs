using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Application.Common.Services;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Domain.Services;
using OnCallendar.Infrastructure.Persistence;

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
        DateTime CreatedAtUtc, DateTime? ResolvedAtUtc);

    private static ShiftBriefDto Brief(Shift s) => new(
        s.Id, s.Date.ToString("yyyy-MM-dd"), s.Code.ToString(), s.StartUtc, s.EndUtc);

    private static SwapDto Map(SwapRequest r) => new(
        r.Id, r.Type, r.Status,
        r.InitiatorMedicoId, $"{r.InitiatorMedico.FirstName} {r.InitiatorMedico.LastName}",
        Brief(r.InitiatorShift),
        r.CounterpartMedicoId,
        r.CounterpartMedico is null ? null : $"{r.CounterpartMedico.FirstName} {r.CounterpartMedico.LastName}",
        r.CounterpartShift is null ? null : Brief(r.CounterpartShift),
        r.Message, r.ResolutionReason, r.CreatedAtUtc, r.ResolvedAtUtc);

    private IQueryable<SwapRequest> LoadSwaps() => _db.SwapRequests
        .Include(r => r.InitiatorMedico)
        .Include(r => r.CounterpartMedico)
        .Include(r => r.InitiatorShift)
        .Include(r => r.CounterpartShift);

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

        await _db.SaveChangesAsync();

        // Notifica al destinatario
        var initiator = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid);
        await SendSwapMailAsync(
            to: to,
            initiator: initiator,
            subject: $"[OnCallendar] Nuova richiesta turno da {FullName(initiator)}",
            body: $"<p>Ciao {to.FirstName},</p>" +
                  $"<p><b>{FullName(initiator)}</b> ti ha proposto di prendere il suo turno " +
                  $"<b>{shift.Code}</b> del <b>{shift.Date:dd/MM/yyyy}</b>.</p>" +
                  (string.IsNullOrWhiteSpace(req.Message) ? string.Empty : $"<p><i>Messaggio:</i> {System.Net.WebUtility.HtmlEncode(req.Message)}</p>") +
                  "<p>Apri l'app OnCallendar per accettare o rifiutare.</p>");

        return Ok(await ReloadDto(swap.Id));
    }

    public sealed record CreateSwapRequest(Guid MyShiftId, Guid OtherShiftId, string? Message);

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

        await _db.SaveChangesAsync();
        return Ok(await ReloadDto(swap.Id));
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

        return await ResolveAcceptance(swap.Id);
    }

    // ---------- ACCEPT / REJECT / CANCEL ----------
    [HttpPost("{id:guid}/accept")]
    public Task<ActionResult<SwapDto>> Accept(Guid id) => ResolveAcceptance(id);

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
        await _db.SaveChangesAsync();

        // Notifica all'iniziatore
        await SendSwapMailAsync(
            to: swap.InitiatorMedico,
            initiator: swap.CounterpartMedico,
            subject: $"[OnCallendar] Richiesta rifiutata da {FullName(swap.CounterpartMedico)}",
            body: $"<p>Ciao {swap.InitiatorMedico.FirstName},</p>" +
                  $"<p><b>{FullName(swap.CounterpartMedico)}</b> ha rifiutato la tua richiesta sul turno " +
                  $"<b>{swap.InitiatorShift.Code}</b> del <b>{swap.InitiatorShift.Date:dd/MM/yyyy}</b>.</p>" +
                  (string.IsNullOrWhiteSpace(body?.Reason) ? string.Empty : $"<p><i>Motivo:</i> {System.Net.WebUtility.HtmlEncode(body!.Reason!)}</p>"));

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
        await _db.SaveChangesAsync();

        // Notifica al counterpart (se c'era un destinatario)
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
    private async Task<ActionResult<SwapDto>> ResolveAcceptance(Guid swapId)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var swap = await LoadSwaps().FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return NotFound();
        if (swap.CounterpartMedicoId != uid) return Forbid();
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
                violations = result.Violations
            });
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

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // Notifica all'iniziatore: la sua richiesta è stata accettata.
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

    // ---------- MAIL HELPERS ----------
    private static string FullName(ApplicationUser? u) =>
        u is null ? "qualcuno" : $"{u.FirstName} {u.LastName}".Trim();

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
