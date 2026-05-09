using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Application.Common.Services;
using OnCallendar.Domain.Entities;
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
            .Include(s => s.MedicoTurno).Include(s => s.MedicoReperibile).Include(s => s.ExternalDoctor)
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
            .Include(s => s.MedicoTurno).Include(s => s.MedicoReperibile).Include(s => s.ExternalDoctor)
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

    public sealed record AssignExternalRequest(string FirstName, string LastName, string? Phone);

    /// <summary>
    /// Assegna il turno a un medico ESTERNO (non utente dell'app). Se il
    /// nome+cognome esistono già nell'anagrafica esterna del tenant viene
    /// riusato il record, altrimenti viene censito ex-novo per essere
    /// riproposto come suggerimento la prossima volta.
    /// </summary>
    [HttpPost("{id:guid}/assign-external")]
    public async Task<ActionResult<ShiftDto>> AssignExternal(Guid id, [FromBody] AssignExternalRequest body)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var first = (body?.FirstName ?? string.Empty).Trim();
        var last  = (body?.LastName  ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
            return BadRequest(new { error = "Nome e cognome del medico esterno sono obbligatori." });

        var s = await _db.Shifts
            .Include(x => x.MedicoTurno).Include(x => x.MedicoReperibile).Include(x => x.ExternalDoctor)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (s.MedicoTurnoId != uid) return Forbid();
        if (s.EndUtc <= DateTime.UtcNow)
            return BadRequest(new { error = "Non puoi modificare un turno già concluso." });

        var key = ExternalDoctor.Normalize(first, last);
        var existing = await _db.ExternalDoctors
            .FirstOrDefaultAsync(e => e.TenantId == s.TenantId && e.NormalizedKey == key);

        ExternalDoctor ext;
        if (existing is not null)
        {
            ext = existing;
            // Aggiorna eventuale telefono se fornito.
            if (!string.IsNullOrWhiteSpace(body?.Phone))
            {
                ext.Phone = body.Phone.Trim();
                ext.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        else
        {
            ext = new ExternalDoctor
            {
                TenantId = s.TenantId,
                FirstName = first,
                LastName = last,
                NormalizedKey = key,
                Phone = string.IsNullOrWhiteSpace(body?.Phone) ? null : body!.Phone.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.ExternalDoctors.Add(ext);
            await _db.SaveChangesAsync();
            _audit.Log("ExternalDoctor", ext.Id, "Created", s.TenantId);
        }

        s.ExternalDoctorId = ext.Id;
        s.ExternalDoctor = ext;
        s.UpdatedAtUtc = DateTime.UtcNow;
        // Il turno esce dalla bacheca: è coperto.
        if (s.Status == ShiftStatus.OnBoard) s.Status = ShiftStatus.Assigned;
        _audit.Log("Shift", s.Id, $"AssignedToExternal:{ext.FullName}", s.TenantId);
        await _db.SaveChangesAsync();

        return Ok(Map(s, uid));
    }

    /// <summary>Rimuove l'assegnazione del turno a un medico esterno (torna al titolare).</summary>
    [HttpPost("{id:guid}/clear-external")]
    public async Task<ActionResult<ShiftDto>> ClearExternal(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var s = await _db.Shifts
            .Include(x => x.MedicoTurno).Include(x => x.MedicoReperibile).Include(x => x.ExternalDoctor)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (s.MedicoTurnoId != uid) return Forbid();
        if (s.ExternalDoctorId is null) return Ok(Map(s, uid));

        var prev = s.ExternalDoctor?.FullName ?? "?";
        s.ExternalDoctorId = null;
        s.ExternalDoctor = null;
        s.UpdatedAtUtc = DateTime.UtcNow;
        _audit.Log("Shift", s.Id, $"ClearedExternal:{prev}", s.TenantId);
        await _db.SaveChangesAsync();
        return Ok(Map(s, uid));
    }

    private static DateOnly? TryParseDate(string? s)
        => DateOnly.TryParse(s, out var d) ? d : null;
}
