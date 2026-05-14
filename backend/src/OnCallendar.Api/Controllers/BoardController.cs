using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Enums;
using static OnCallendar.Api.Controllers.ShiftDtos;

namespace OnCallendar.Api.Controllers;

/// <summary>
/// Bacheca: turni futuri pubblicati dal medico di turno cercando un sostituto.
/// </summary>
[ApiController]
[Route("api/board")]
[Authorize]
public sealed class BoardController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public BoardController(IApplicationDbContext db, ICurrentUserService user)
    {
        _db = db; _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShiftDto>>> Get()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        // Filtro mese corrente (Europe/Rome): in bacheca compaiono solo i turni
        // ancora futuri E che cadono entro la fine del mese in corso.
        var now = DateTime.UtcNow;
        TimeZoneInfo rome;
        try { rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }
        catch { try { rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"); } catch { rome = TimeZoneInfo.Utc; } }
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(now, rome);
        var monthEndLocal = new DateOnly(nowLocal.Year, nowLocal.Month, DateTime.DaysInMonth(nowLocal.Year, nowLocal.Month));

        var items = await _db.Shifts
            .Include(s => s.MedicoTurno)
            .Include(s => s.MedicoReperibile)
            .Include(s => s.ExternalDoctor)
            .Include(s => s.ExternalDoctorReperibile)
            .Where(s => s.Status == ShiftStatus.OnBoard
                        && s.StartUtc > now
                        && s.Date <= monthEndLocal)
            .OrderBy(s => s.StartUtc)
            .ToListAsync();

        return Ok(items.Select(s => Map(s, uid)));
    }
}
