using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Persistence;

namespace OnCallendar.Api.Controllers;

[ApiController]
[Route("api/board")]
[Authorize]
public sealed class BoardController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public BoardController(ApplicationDbContext db) => _db = db;

    public sealed record BoardItemDto(
        Guid ShiftId, DateTime StartUtc, DateTime EndUtc,
        string? Location, string? Notes,
        Guid OfferedByMedicoId, string OfferedByMedicoName);

    /// <summary>Tutti i turni pubblicati sulla bacheca del tenant.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BoardItemDto>>> Get()
    {
        var items = await _db.Shifts
            .Include(s => s.Assignments).ThenInclude(a => a.Medico)
            .Where(s => s.Status == ShiftStatus.OnBoard && s.StartUtc > DateTime.UtcNow)
            .OrderBy(s => s.StartUtc)
            .ToListAsync();

        var dto = items.Select(s =>
        {
            var current = s.Assignments.First(a => a.IsCurrent && !a.IsDeleted);
            return new BoardItemDto(
                s.Id, s.StartUtc, s.EndUtc, s.Location, s.Notes,
                current.MedicoId, $"{current.Medico.FirstName} {current.Medico.LastName}");
        });

        return Ok(dto);
    }
}
