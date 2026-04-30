using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Application.Common.Services;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Persistence;
using static OnCallendar.Api.Controllers.ShiftDtos;

namespace OnCallendar.Api.Controllers;

/// <summary>
/// Lettura dei turni e azioni di pubblicazione/ritiro dalla bacheca.
/// </summary>
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

    /// <summary>Tutti i turni in finestra (default ±30gg).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShiftDto>>> GetAll(
        [FromQuery] string? from, [FromQuery] string? to)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var fromDate = TryParseDate(from) ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);
        var toDate   = TryParseDate(to)   ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

        var list = await _db.Shifts
            .Include(s => s.MedicoTurno).Include(s => s.MedicoReperibile)
            .Where(s => s.Date >= fromDate && s.Date <= toDate)
            .OrderBy(s => s.StartUtc)
            .ToListAsync();
        return Ok(list.Select(s => Map(s, uid)));
    }

    /// <summary>I miei turni (come Medico di Turno o Reperibile).</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ShiftDto>>> Mine(
        [FromQuery] string? from, [FromQuery] string? to)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var fromDate = TryParseDate(from) ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        var toDate   = TryParseDate(to)   ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);

        var list = await _db.Shifts
            .Include(s => s.MedicoTurno).Include(s => s.MedicoReperibile)
            .Where(s => s.Date >= fromDate && s.Date <= toDate &&
                        (s.MedicoTurnoId == uid || s.MedicoReperibileId == uid))
            .OrderBy(s => s.StartUtc)
            .ToListAsync();
        return Ok(list.Select(s => Map(s, uid)));
    }

    /// <summary>Pubblica il proprio turno sulla bacheca.</summary>
    [HttpPost("{id:guid}/publish-on-board")]
    public async Task<IActionResult> PublishOnBoard(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var s = await _db.Shifts.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (s.MedicoTurnoId != uid) return Forbid();
        if (s.StartUtc <= DateTime.UtcNow)
            return BadRequest(new { error = "Non puoi pubblicare un turno già iniziato." });
        if (s.Status == ShiftStatus.OnBoard)
            return Ok(Map(s, uid));

        s.Status = ShiftStatus.OnBoard;
        s.UpdatedAtUtc = DateTime.UtcNow;
        _audit.Log("Shift", s.Id, "PublishedOnBoard", s.TenantId);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Ritira il proprio turno dalla bacheca.</summary>
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var s = await _db.Shifts.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (s.MedicoTurnoId != uid) return Forbid();
        if (s.Status != ShiftStatus.OnBoard)
            return BadRequest(new { error = "Il turno non è in bacheca." });

        s.Status = ShiftStatus.Assigned;
        s.UpdatedAtUtc = DateTime.UtcNow;
        _audit.Log("Shift", s.Id, "UnpublishedFromBoard", s.TenantId);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static DateOnly? TryParseDate(string? s)
        => DateOnly.TryParse(s, out var d) ? d : null;
}
