using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Infrastructure.Persistence;
using OnCallendar.Domain.Enums;

namespace OnCallendar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Restituisce statistiche aggregate per il dashboard personale.
    /// Query params: year, month (default = mese corrente).
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int? year, [FromQuery] int? month)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var filterYear = year ?? DateTime.UtcNow.Year;
        var filterMonth = month ?? DateTime.UtcNow.Month;
        var from = new DateTime(filterYear, filterMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        // Turni dell'utente nel mese
        var myShifts = await _db.Shifts
            .Where(s => s.StartUtc >= from && s.StartUtc < to)
            .Where(s => s.MedicoTurnoId == userId || s.MedicoReperibileId == userId)
            .ToListAsync();

        // Ore totali lavorate (come titolare)
        var hoursWorked = myShifts
            .Where(s => s.MedicoTurnoId == userId)
            .Sum(s => (s.EndUtc - s.StartUtc).TotalHours);

        // Conteggio turni per tipo
        var shiftsByCode = myShifts
            .Where(s => s.MedicoTurnoId == userId)
            .GroupBy(s => s.Code)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // Stima stipendio (esempio: €25/ora, personalizzabile)
        var hourlyRate = 25m;
        var estimatedSalary = (decimal)hoursWorked * hourlyRate;

        // Modifiche recenti (swap accettati questo mese)
        var recentChanges = await _db.SwapRequests
            .Where(sr => sr.CreatedAtUtc >= from && sr.CreatedAtUtc < to)
            .Where(sr => sr.InitiatorMedicoId == userId || sr.CounterpartMedicoId == userId)
            .Where(sr => sr.Status == SwapRequestStatus.AutoApproved)
            .CountAsync();

        // Turni reperibilità
        var onCallShifts = myShifts.Count(s => s.MedicoReperibileId == userId);

        return Ok(new
        {
            year = filterYear,
            month = filterMonth,
            totalShifts = myShifts.Count(s => s.MedicoTurnoId == userId),
            hoursWorked = Math.Round(hoursWorked, 1),
            shiftsByCode,
            estimatedSalary = Math.Round(estimatedSalary, 2),
            recentChanges,
            onCallShifts,
        });
    }

    /// <summary>
    /// Dati per grafico mensile: ore lavorate per giorno del mese.
    /// Restituisce array [1..31] con ore lavorate ogni giorno.
    /// </summary>
    [HttpGet("monthly-hours")]
    public async Task<IActionResult> GetMonthlyHours([FromQuery] int? year, [FromQuery] int? month)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var filterYear = year ?? DateTime.UtcNow.Year;
        var filterMonth = month ?? DateTime.UtcNow.Month;
        var from = new DateTime(filterYear, filterMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(filterYear, filterMonth);

        var myShifts = await _db.Shifts
            .Where(s => s.StartUtc >= from && s.StartUtc < to)
            .Where(s => s.MedicoTurnoId == userId)
            .ToListAsync();

        // Raggruppa per giorno del mese
        var hoursByDay = new double[daysInMonth];
        foreach (var shift in myShifts)
        {
            var day = shift.Date.Day - 1; // 0-based index
            if (day >= 0 && day < daysInMonth)
            {
                hoursByDay[day] += (shift.EndUtc - shift.StartUtc).TotalHours;
            }
        }

        return Ok(new
        {
            year = filterYear,
            month = filterMonth,
            daysInMonth,
            hoursByDay = hoursByDay.Select(h => Math.Round(h, 1)).ToArray(),
        });
    }
}
