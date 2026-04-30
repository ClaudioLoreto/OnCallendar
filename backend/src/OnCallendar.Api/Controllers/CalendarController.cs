using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Application.Common.Services;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Persistence;

namespace OnCallendar.Api.Controllers;

[ApiController]
[Route("api/calendar")]
[Authorize]
public sealed class CalendarController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IAuditLogger _audit;

    public CalendarController(ApplicationDbContext db, ICurrentUserService user, IAuditLogger audit)
    {
        _db = db; _user = user; _audit = audit;
    }

    public sealed record AssigneeDto(Guid MedicoId, string FullName, string? AvatarUrl);
    public sealed record SlotDto(
        Guid ShiftId, DateTime StartUtc, DateTime EndUtc, int Capacity,
        string Label, ShiftStatus Status,
        IReadOnlyList<AssigneeDto> Assignees,
        bool IsMine, bool HasFreeSpot);
    public sealed record DayDto(DateTime DateUtc, IReadOnlyList<SlotDto> Slots);

    /// <summary>Vista a giorni con i 2 slot da 12h già aggregati.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DayDto>>> Get(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var fromUtc = (from ?? DateTime.UtcNow.Date).ToUniversalTime();
        var toUtc   = (to   ?? DateTime.UtcNow.Date.AddDays(15)).ToUniversalTime();

        var shifts = await _db.Shifts
            .Include(s => s.Assignments).ThenInclude(a => a.Medico)
            .Where(s => s.StartUtc < toUtc && s.EndUtc > fromUtc)
            .OrderBy(s => s.StartUtc)
            .ToListAsync();

        var grouped = shifts
            .GroupBy(s => s.StartUtc.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DayDto(
                g.Key,
                g.OrderBy(s => s.StartUtc).Select(s => MapSlot(s, uid)).ToList()))
            .ToList();

        return Ok(grouped);
    }

    private static SlotDto MapSlot(Shift s, Guid uid)
    {
        var current = s.Assignments
            .Where(a => a.IsCurrent && !a.IsDeleted)
            .Select(a => new AssigneeDto(a.MedicoId, $"{a.Medico.FirstName} {a.Medico.LastName}", a.Medico.AvatarUrl))
            .ToList();

        var hourLocal = s.StartUtc.ToLocalTime().Hour;
        var label = hourLocal < 12 ? "Mattina (00–12)" : "Notte (12–24)";
        return new SlotDto(
            s.Id, s.StartUtc, s.EndUtc, s.Capacity, label, s.Status,
            current,
            IsMine: current.Any(a => a.MedicoId == uid),
            HasFreeSpot: current.Count < s.Capacity);
    }

    public sealed record JoinSlotsRequest(IReadOnlyList<Guid> ShiftIds);

    /// <summary>Prenota uno o più slot per il medico loggato (se c'è capacità).</summary>
    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinSlotsRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        if (req.ShiftIds is null || req.ShiftIds.Count == 0)
            return BadRequest(new { error = "Nessuno slot selezionato." });

        var shifts = await _db.Shifts
            .Include(s => s.Assignments)
            .Where(s => req.ShiftIds.Contains(s.Id))
            .ToListAsync();
        if (shifts.Count != req.ShiftIds.Count)
            return NotFound(new { error = "Uno o più slot non trovati." });

        var nowUtc = DateTime.UtcNow;
        foreach (var s in shifts)
        {
            if (s.StartUtc <= nowUtc)
                return BadRequest(new { error = $"Slot già iniziato: {s.StartUtc:dd/MM HH:mm}" });
            var current = s.Assignments.Where(a => a.IsCurrent && !a.IsDeleted).ToList();
            if (current.Any(a => a.MedicoId == uid))
                return Conflict(new { error = "Sei già prenotato su questo slot." });
            if (current.Count >= s.Capacity)
                return Conflict(new { error = "Slot al completo." });
        }

        foreach (var s in shifts)
        {
            _db.ShiftAssignments.Add(new ShiftAssignment
            {
                TenantId = s.TenantId,
                ShiftId = s.Id,
                MedicoId = uid,
                IsCurrent = true,
                AssignedAtUtc = nowUtc,
                CreatedAtUtc = nowUtc,
            });
            _audit.Log("ShiftAssignment", s.Id, "SelfJoined", s.TenantId,
                newValues: new { MedicoId = uid });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
