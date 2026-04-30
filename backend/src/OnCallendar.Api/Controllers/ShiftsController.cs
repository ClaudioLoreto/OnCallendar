using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Application.Common.Services;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Persistence;
using OnCallendar.Infrastructure.Persistence.Seed;

namespace OnCallendar.Api.Controllers;

[ApiController]
[Route("api/shifts")]
[Authorize]
public sealed class ShiftsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IAuditLogger _audit;

    public ShiftsController(ApplicationDbContext db, ICurrentUserService user, IAuditLogger audit)
    {
        _db = db; _user = user; _audit = audit;
    }

    public sealed record ShiftDto(
        Guid Id, DateTime StartUtc, DateTime EndUtc, string? Location,
        string? Notes, ShiftStatus Status,
        Guid? AssignedMedicoId, string? AssignedMedicoName);

    private static ShiftDto Map(Shift s)
    {
        var current = s.Assignments.FirstOrDefault(a => a.IsCurrent && !a.IsDeleted);
        return new ShiftDto(
            s.Id, s.StartUtc, s.EndUtc, s.Location, s.Notes, s.Status,
            current?.MedicoId,
            current?.Medico is null ? null : $"{current.Medico.FirstName} {current.Medico.LastName}");
    }

    /// <summary>Tutti i turni del tenant in una finestra temporale.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShiftDto>>> GetAll(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromUtc = (from ?? DateTime.UtcNow.AddDays(-7)).ToUniversalTime();
        var toUtc   = (to   ?? DateTime.UtcNow.AddDays(30)).ToUniversalTime();

        var list = await _db.Shifts
            .Include(s => s.Assignments).ThenInclude(a => a.Medico)
            .Where(s => s.StartUtc < toUtc && s.EndUtc > fromUtc)
            .OrderBy(s => s.StartUtc)
            .ToListAsync();

        return Ok(list.Select(Map));
    }

    /// <summary>Solo i turni attualmente assegnati al medico loggato.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ShiftDto>>> Mine(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var fromUtc = (from ?? DateTime.UtcNow.AddDays(-7)).ToUniversalTime();
        var toUtc   = (to   ?? DateTime.UtcNow.AddDays(30)).ToUniversalTime();

        var list = await _db.Shifts
            .Include(s => s.Assignments).ThenInclude(a => a.Medico)
            .Where(s => s.StartUtc < toUtc && s.EndUtc > fromUtc &&
                        s.Assignments.Any(a => a.IsCurrent && !a.IsDeleted && a.MedicoId == uid))
            .OrderBy(s => s.StartUtc)
            .ToListAsync();

        return Ok(list.Select(Map));
    }

    public sealed record CreateShiftRequest(
        DateTime StartUtc, DateTime EndUtc, Guid AssignToMedicoId,
        string? Location, string? Notes);

    /// <summary>Crea un nuovo turno e lo assegna a un medico (admin only per ora).</summary>
    [HttpPost]
    [Authorize(Roles = DbSeeder.SuperAdminRole)]
    public async Task<ActionResult<ShiftDto>> Create([FromBody] CreateShiftRequest req)
    {
        var medico = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == req.AssignToMedicoId && !u.IsDeleted);
        if (medico is null || medico.TenantId is null)
            return BadRequest(new { error = "Medico inesistente o senza tenant." });

        var tenantId = medico.TenantId.Value;

        var shift = new Shift
        {
            TenantId = tenantId,
            StartUtc = req.StartUtc.ToUniversalTime(),
            EndUtc = req.EndUtc.ToUniversalTime(),
            Location = req.Location,
            Notes = req.Notes,
            Status = ShiftStatus.Assigned,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Shifts.Add(shift);

        var assign = new ShiftAssignment
        {
            TenantId = tenantId,
            Shift = shift,
            MedicoId = medico.Id,
            IsCurrent = true,
            AssignedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.ShiftAssignments.Add(assign);

        _audit.Log("Shift", shift.Id, "Created", tenantId,
            newValues: new { shift.StartUtc, shift.EndUtc, MedicoId = medico.Id });

        await _db.SaveChangesAsync();

        // ricarico per restituire DTO completo
        await _db.Entry(assign).Reference(a => a.Medico).LoadAsync();
        shift.Assignments.Add(assign);
        return Ok(Map(shift));
    }

    /// <summary>Pubblica il turno sulla bacheca: cerco un sostituto.</summary>
    [HttpPost("{id:guid}/publish-on-board")]
    public async Task<IActionResult> PublishOnBoard(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var shift = await _db.Shifts
            .Include(s => s.Assignments)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (shift is null) return NotFound();

        var current = shift.Assignments.FirstOrDefault(a => a.IsCurrent && !a.IsDeleted);
        if (current?.MedicoId != uid)
            return Forbid();

        if (shift.StartUtc <= DateTime.UtcNow)
            return BadRequest(new { error = "Non puoi pubblicare un turno già iniziato." });

        shift.Status = ShiftStatus.OnBoard;
        shift.UpdatedAtUtc = DateTime.UtcNow;

        _audit.Log("Shift", shift.Id, "PublishedOnBoard", shift.TenantId,
            newValues: new { Status = shift.Status.ToString() });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Ritira il turno dalla bacheca.</summary>
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var shift = await _db.Shifts
            .Include(s => s.Assignments)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (shift is null) return NotFound();

        var current = shift.Assignments.FirstOrDefault(a => a.IsCurrent && !a.IsDeleted);
        if (current?.MedicoId != uid) return Forbid();

        if (shift.Status != ShiftStatus.OnBoard)
            return BadRequest(new { error = "Il turno non è in bacheca." });

        shift.Status = ShiftStatus.Assigned;
        shift.UpdatedAtUtc = DateTime.UtcNow;

        _audit.Log("Shift", shift.Id, "UnpublishedFromBoard", shift.TenantId);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
