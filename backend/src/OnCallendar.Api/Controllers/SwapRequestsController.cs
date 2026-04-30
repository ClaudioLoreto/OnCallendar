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

[ApiController]
[Route("api/swaps")]
[Authorize]
public sealed class SwapRequestsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IShiftValidationService _rules;
    private readonly IAuditLogger _audit;

    public SwapRequestsController(
        ApplicationDbContext db, ICurrentUserService user,
        IShiftValidationService rules, IAuditLogger audit)
    {
        _db = db; _user = user; _rules = rules; _audit = audit;
    }

    public sealed record SwapDto(
        Guid Id, SwapRequestType Type, SwapRequestStatus Status,
        Guid InitiatorId, string InitiatorName,
        Guid InitiatorShiftId, DateTime InitiatorShiftStart, DateTime InitiatorShiftEnd,
        Guid? CounterpartId, string? CounterpartName,
        Guid? CounterpartShiftId, DateTime? CounterpartShiftStart, DateTime? CounterpartShiftEnd,
        string? Message, string? ResolutionReason,
        DateTime CreatedAtUtc, DateTime? ResolvedAtUtc);

    private static SwapDto Map(SwapRequest r) => new(
        r.Id, r.Type, r.Status,
        r.InitiatorMedicoId, $"{r.InitiatorMedico.FirstName} {r.InitiatorMedico.LastName}",
        r.InitiatorShiftId, r.InitiatorShift.StartUtc, r.InitiatorShift.EndUtc,
        r.CounterpartMedicoId,
        r.CounterpartMedico is null ? null : $"{r.CounterpartMedico.FirstName} {r.CounterpartMedico.LastName}",
        r.CounterpartShiftId, r.CounterpartShift?.StartUtc, r.CounterpartShift?.EndUtc,
        r.Message, r.ResolutionReason, r.CreatedAtUtc, r.ResolvedAtUtc);

    // -------------------- LIST --------------------

    [HttpGet("incoming")]
    public async Task<ActionResult<IEnumerable<SwapDto>>> Incoming()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var list = await LoadSwaps()
            .Where(r => r.CounterpartMedicoId == uid && r.Status == SwapRequestStatus.Pending)
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
            .Take(50)
            .ToListAsync();
        return Ok(list.Select(Map));
    }

    private IQueryable<SwapRequest> LoadSwaps() => _db.SwapRequests
        .Include(r => r.InitiatorMedico)
        .Include(r => r.CounterpartMedico)
        .Include(r => r.InitiatorShift)
        .Include(r => r.CounterpartShift);

    // -------------------- CREATE --------------------

    public sealed record CreateGiveawayRequest(Guid ShiftId, Guid ToMedicoId, string? Message);

    /// <summary>Cessione diretta del proprio turno a un collega.</summary>
    [HttpPost("giveaway")]
    public async Task<ActionResult<SwapDto>> CreateGiveaway([FromBody] CreateGiveawayRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var shift = await _db.Shifts
            .Include(s => s.Assignments).ThenInclude(a => a.Medico)
            .FirstOrDefaultAsync(s => s.Id == req.ShiftId);
        if (shift is null) return NotFound(new { error = "Turno inesistente." });

        var current = shift.Assignments.FirstOrDefault(a => a.IsCurrent && !a.IsDeleted);
        if (current?.MedicoId != uid)
            return Forbid();

        var to = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.ToMedicoId);
        if (to is null) return BadRequest(new { error = "Destinatario inesistente." });
        if (to.Id == uid) return BadRequest(new { error = "Non puoi cedere il turno a te stesso." });

        // Regola: max 1 richiesta pending per turno (di qualunque tipo) iniziata da me
        var alreadyPending = await _db.SwapRequests.AnyAsync(r =>
            r.InitiatorMedicoId == uid &&
            r.InitiatorShiftId == shift.Id &&
            r.Status == SwapRequestStatus.Pending);
        if (alreadyPending)
            return Conflict(new { error = "Hai già una richiesta di scambio in sospeso per questo turno." });

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
        return Ok(await ReloadDto(swap.Id));
    }

    public sealed record CreateSwapRequest(Guid MyShiftId, Guid OtherShiftId, string? Message);

    /// <summary>Scambio bilaterale: io offro myShift in cambio di otherShift.</summary>
    [HttpPost("swap")]
    public async Task<ActionResult<SwapDto>> CreateSwap([FromBody] CreateSwapRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var mine = await _db.Shifts.Include(s => s.Assignments)
            .FirstOrDefaultAsync(s => s.Id == req.MyShiftId);
        var other = await _db.Shifts.Include(s => s.Assignments).ThenInclude(a => a.Medico)
            .FirstOrDefaultAsync(s => s.Id == req.OtherShiftId);
        if (mine is null || other is null) return NotFound();

        var mineCurr  = mine.Assignments.FirstOrDefault(a => a.IsCurrent && !a.IsDeleted);
        var otherCurr = other.Assignments.FirstOrDefault(a => a.IsCurrent && !a.IsDeleted);
        if (mineCurr?.MedicoId != uid) return Forbid();
        if (otherCurr is null) return BadRequest(new { error = "Turno di destinazione non assegnato." });
        if (otherCurr.MedicoId == uid) return BadRequest(new { error = "Non puoi scambiare con te stesso." });

        var swap = new SwapRequest
        {
            TenantId = mine.TenantId,
            Type = SwapRequestType.Swap,
            Status = SwapRequestStatus.Pending,
            InitiatorMedicoId = uid,
            InitiatorShiftId = mine.Id,
            CounterpartMedicoId = otherCurr.MedicoId,
            CounterpartShiftId = other.Id,
            Message = req.Message,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.SwapRequests.Add(swap);
        _audit.Log("SwapRequest", swap.Id, "SwapCreated", mine.TenantId,
            newValues: new { swap.InitiatorShiftId, swap.CounterpartShiftId, swap.CounterpartMedicoId });

        await _db.SaveChangesAsync();
        return Ok(await ReloadDto(swap.Id));
    }

    /// <summary>Pesco un turno dalla bacheca: il Rule Engine valida e auto-approva.</summary>
    [HttpPost("pick/{shiftId:guid}")]
    public async Task<ActionResult<SwapDto>> PickFromBoard(Guid shiftId)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var shift = await _db.Shifts
            .Include(s => s.Assignments).ThenInclude(a => a.Medico)
            .FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift is null) return NotFound();
        if (shift.Status != ShiftStatus.OnBoard)
            return BadRequest(new { error = "Turno non in bacheca." });

        var current = shift.Assignments.First(a => a.IsCurrent && !a.IsDeleted);
        if (current.MedicoId == uid) return BadRequest(new { error = "Sei già tu l'assegnatario." });

        var swap = new SwapRequest
        {
            TenantId = shift.TenantId,
            Type = SwapRequestType.PickFromBoard,
            Status = SwapRequestStatus.Pending,
            InitiatorMedicoId = current.MedicoId,   // chi cede (proprietario originale)
            InitiatorShiftId = shift.Id,
            CounterpartMedicoId = uid,              // chi prende
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.SwapRequests.Add(swap);
        await _db.SaveChangesAsync();

        // Pick = auto-accept lato destinatario
        return await ResolveAcceptance(swap.Id);
    }

    // -------------------- ACCEPT / REJECT --------------------

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<SwapDto>> Accept(Guid id) => await ResolveAcceptance(id);

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<SwapDto>> Reject(Guid id, [FromBody] RejectBody? body)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var swap = await _db.SwapRequests
            .Include(r => r.InitiatorMedico).Include(r => r.CounterpartMedico)
            .Include(r => r.InitiatorShift).Include(r => r.CounterpartShift)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (swap is null) return NotFound();
        if (swap.CounterpartMedicoId != uid) return Forbid();
        if (swap.Status != SwapRequestStatus.Pending)
            return BadRequest(new { error = "Richiesta non più pendente." });

        swap.Status = SwapRequestStatus.Rejected;
        swap.ResolvedAtUtc = DateTime.UtcNow;
        swap.ResolutionReason = body?.Reason;
        _audit.Log("SwapRequest", swap.Id, "Rejected", swap.TenantId, notes: body?.Reason);
        await _db.SaveChangesAsync();
        return Ok(Map(swap));
    }
    public sealed record RejectBody(string? Reason);

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SwapDto>> Cancel(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var swap = await _db.SwapRequests
            .Include(r => r.InitiatorMedico).Include(r => r.CounterpartMedico)
            .Include(r => r.InitiatorShift).Include(r => r.CounterpartShift)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (swap is null) return NotFound();
        if (swap.InitiatorMedicoId != uid) return Forbid();
        if (swap.Status != SwapRequestStatus.Pending)
            return BadRequest(new { error = "Richiesta non più pendente." });

        swap.Status = SwapRequestStatus.Cancelled;
        swap.ResolvedAtUtc = DateTime.UtcNow;
        _audit.Log("SwapRequest", swap.Id, "Cancelled", swap.TenantId);
        await _db.SaveChangesAsync();
        return Ok(Map(swap));
    }

    // -------------------- CORE: validazione + applicazione --------------------

    private async Task<ActionResult<SwapDto>> ResolveAcceptance(Guid swapId)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var swap = await _db.SwapRequests
            .Include(r => r.InitiatorMedico)
            .Include(r => r.CounterpartMedico)
            .Include(r => r.InitiatorShift).ThenInclude(s => s.Assignments)
            .Include(r => r.CounterpartShift).ThenInclude(s => s!.Assignments)
            .FirstOrDefaultAsync(r => r.Id == swapId);
        if (swap is null) return NotFound();
        if (swap.CounterpartMedicoId != uid) return Forbid();
        if (swap.Status != SwapRequestStatus.Pending)
            return BadRequest(new { error = "Richiesta non più pendente." });

        // Carico i turni "vicini" del/i medico/i nei prossimi 7gg attorno al/i turno/i scambiato/i
        var window = TimeSpan.FromDays(7);
        var initiatorId = swap.InitiatorMedicoId;
        var counterpartId = swap.CounterpartMedicoId!.Value;

        var minStart = swap.InitiatorShift.StartUtc - window;
        var maxEnd   = swap.InitiatorShift.EndUtc + window;
        if (swap.CounterpartShift is not null)
        {
            if (swap.CounterpartShift.StartUtc - window < minStart) minStart = swap.CounterpartShift.StartUtc - window;
            if (swap.CounterpartShift.EndUtc + window > maxEnd)     maxEnd   = swap.CounterpartShift.EndUtc + window;
        }

        var counterpartShifts = await _db.Shifts
            .Where(s => s.Assignments.Any(a => a.IsCurrent && !a.IsDeleted && a.MedicoId == counterpartId)
                        && s.StartUtc < maxEnd && s.EndUtc > minStart
                        && s.Status != ShiftStatus.Cancelled)
            .ToListAsync();

        ShiftValidationResult result;

        if (swap.Type == SwapRequestType.Swap && swap.CounterpartShift is not null)
        {
            var initiatorShifts = await _db.Shifts
                .Where(s => s.Assignments.Any(a => a.IsCurrent && !a.IsDeleted && a.MedicoId == initiatorId)
                            && s.StartUtc < maxEnd && s.EndUtc > minStart
                            && s.Status != ShiftStatus.Cancelled)
                .ToListAsync();

            result = _rules.ValidateSwap(
                initiatorId,   swap.InitiatorShift,   initiatorShifts,
                counterpartId, swap.CounterpartShift, counterpartShifts);
        }
        else
        {
            // Giveaway o PickFromBoard: trasferisco InitiatorShift al counterpart
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

        // ------- APPLICAZIONE ATOMICA -------
        await using var tx = await _db.Database.BeginTransactionAsync();

        ReassignShift(swap.InitiatorShift, fromMedicoId: initiatorId, toMedicoId: counterpartId, swap.Id);

        if (swap.Type == SwapRequestType.Swap && swap.CounterpartShift is not null)
            ReassignShift(swap.CounterpartShift, fromMedicoId: counterpartId, toMedicoId: initiatorId, swap.Id);

        // Se il turno era OnBoard riportalo a Assigned
        if (swap.InitiatorShift.Status == ShiftStatus.OnBoard)
            swap.InitiatorShift.Status = ShiftStatus.Assigned;

        swap.Status = SwapRequestStatus.AutoApproved;
        swap.ResolvedAtUtc = DateTime.UtcNow;

        _audit.Log("SwapRequest", swap.Id, "AutoApproved", swap.TenantId,
            newValues: new { swap.Type, swap.InitiatorShiftId, swap.CounterpartShiftId });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(await ReloadDto(swap.Id));
    }

    private void ReassignShift(Shift shift, Guid fromMedicoId, Guid toMedicoId, Guid swapId)
    {
        var current = shift.Assignments.FirstOrDefault(a => a.IsCurrent && !a.IsDeleted);
        if (current is not null)
        {
            current.IsCurrent = false;
            current.UpdatedAtUtc = DateTime.UtcNow;
        }
        _db.ShiftAssignments.Add(new ShiftAssignment
        {
            TenantId = shift.TenantId,
            ShiftId = shift.Id,
            MedicoId = toMedicoId,
            IsCurrent = true,
            AssignedAtUtc = DateTime.UtcNow,
            OriginatingSwapRequestId = swapId,
            CreatedAtUtc = DateTime.UtcNow,
        });
        _audit.Log("ShiftAssignment", shift.Id, "Reassigned", shift.TenantId,
            oldValues: new { MedicoId = fromMedicoId },
            newValues: new { MedicoId = toMedicoId, SwapId = swapId });
    }

    private async Task<SwapDto> ReloadDto(Guid id)
    {
        var fresh = await LoadSwaps().FirstAsync(r => r.Id == id);
        return Map(fresh);
    }
}
