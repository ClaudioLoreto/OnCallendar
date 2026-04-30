using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Infrastructure.Persistence;
using static OnCallendar.Api.Controllers.ShiftDtos;

namespace OnCallendar.Api.Controllers;

/// <summary>
/// Vista calendario: turni del tenant (Navelli) raggruppati per data,
/// con Medico di Turno e Medico Reperibile.
/// </summary>
[ApiController]
[Route("api/calendar")]
[Authorize]
public sealed class CalendarController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public CalendarController(ApplicationDbContext db, ICurrentUserService user)
    {
        _db = db; _user = user;
    }

    public sealed record DayDto(string Date, IReadOnlyList<ShiftDto> Shifts);

    /// <summary>
    /// Restituisce i turni nella finestra [from, to] (default: oggi → +14 giorni).
    /// Le date sono in formato yyyy-MM-dd locale (Europe/Rome).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DayDto>>> Get(
        [FromQuery] string? from, [FromQuery] string? to)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var fromDate = TryParseDate(from) ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var toDate   = TryParseDate(to)   ?? fromDate.AddDays(14);
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);

        var shifts = await _db.Shifts
            .Include(s => s.MedicoTurno)
            .Include(s => s.MedicoReperibile)
            .Where(s => s.Date >= fromDate && s.Date <= toDate)
            .OrderBy(s => s.Date).ThenBy(s => s.StartUtc)
            .ToListAsync();

        var grouped = shifts
            .GroupBy(s => s.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DayDto(
                g.Key.ToString("yyyy-MM-dd"),
                g.Select(s => Map(s, uid)).ToList()))
            .ToList();

        return Ok(grouped);
    }

    private static DateOnly? TryParseDate(string? s)
        => DateOnly.TryParse(s, out var d) ? d : null;
}
