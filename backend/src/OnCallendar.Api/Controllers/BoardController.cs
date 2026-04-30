using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Persistence;
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
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public BoardController(ApplicationDbContext db, ICurrentUserService user)
    {
        _db = db; _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShiftDto>>> Get()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var nowUtc = DateTime.UtcNow;
        var items = await _db.Shifts
            .Include(s => s.MedicoTurno)
            .Include(s => s.MedicoReperibile)
            .Where(s => s.Status == ShiftStatus.OnBoard && s.StartUtc > nowUtc)
            .OrderBy(s => s.StartUtc)
            .ToListAsync();

        return Ok(items.Select(s => Map(s, uid)));
    }
}
