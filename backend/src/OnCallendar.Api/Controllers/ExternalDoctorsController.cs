using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Infrastructure.Persistence;

namespace OnCallendar.Api.Controllers;

/// <summary>
/// Anagrafica dei medici ESTERNI (non utenti dell'app), usata per
/// l'autocomplete quando si cede un turno a una persona non censita.
/// </summary>
[ApiController]
[Route("api/external-doctors")]
[Authorize]
public sealed class ExternalDoctorsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public ExternalDoctorsController(ApplicationDbContext db, ICurrentUserService user)
    {
        _db = db; _user = user;
    }

    public sealed record ExternalDoctorDto(Guid Id, string FirstName, string LastName, string FullName, string? Phone);

    /// <summary>
    /// Suggerimenti per autocomplete. Filtra per match (case-insensitive)
    /// del termine su nome o cognome. Restituisce al massimo 10 risultati.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ExternalDoctorDto>>> Search([FromQuery] string? q)
    {
        if (_user.UserId is null) return Unauthorized();

        var query = _db.ExternalDoctors.AsNoTracking();
        var term = (q ?? string.Empty).Trim();
        if (term.Length > 0)
        {
            var like = $"%{term.ToLowerInvariant()}%";
            query = query.Where(e =>
                EF.Functions.ILike(e.FirstName, like) ||
                EF.Functions.ILike(e.LastName, like) ||
                EF.Functions.ILike(e.NormalizedKey, like));
        }

        var list = await query
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Take(10)
            .ToListAsync();

        return Ok(list.Select(e => new ExternalDoctorDto(
            e.Id, e.FirstName, e.LastName, e.FullName, e.Phone)));
    }
}
