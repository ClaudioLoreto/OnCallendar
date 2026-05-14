using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Persistence;

namespace OnCallendar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Export Excel dello storico turni personale (utente loggato).
    /// Query params: year, month (opzionali, default = mese corrente).
    /// </summary>
    [HttpGet("my-history-excel")]
    public async Task<IActionResult> ExportMyHistory([FromQuery] int? year, [FromQuery] int? month)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var filterYear = year ?? DateTime.UtcNow.Year;
        var filterMonth = month ?? DateTime.UtcNow.Month;
        var from = new DateTime(filterYear, filterMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        var shifts = await _db.Shifts
            .Include(s => s.MedicoTurno)
            .Include(s => s.MedicoReperibile)
            .Where(s => s.StartUtc >= from && s.StartUtc < to)
            .Where(s => s.MedicoTurnoId == userId || s.MedicoReperibileId == userId)
            .OrderBy(s => s.StartUtc)
            .ToListAsync();

        var user = await _db.Users.FindAsync(userId);
        var fileName = $"Storico_{user?.FirstName}_{user?.LastName}_{filterYear}_{filterMonth:D2}.xlsx";

        return GenerateExcel(shifts, fileName, $"I miei turni - {from:MMMM yyyy}");
    }

    /// <summary>
    /// Export Excel dello storico completo (tutti i turni).
    /// Query params: year, month (opzionali, default = mese corrente).
    /// </summary>
    [HttpGet("all-history-excel")]
    public async Task<IActionResult> ExportAllHistory([FromQuery] int? year, [FromQuery] int? month)
    {
        var filterYear = year ?? DateTime.UtcNow.Year;
        var filterMonth = month ?? DateTime.UtcNow.Month;
        var from = new DateTime(filterYear, filterMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        var shifts = await _db.Shifts
            .Include(s => s.MedicoTurno)
            .Include(s => s.MedicoReperibile)
            .Include(s => s.ExternalDoctor)
            .Include(s => s.ExternalDoctorReperibile)
            .Where(s => s.StartUtc >= from && s.StartUtc < to)
            .OrderBy(s => s.StartUtc)
            .ToListAsync();

        var fileName = $"Storico_Completo_{filterYear}_{filterMonth:D2}.xlsx";

        return GenerateExcel(shifts, fileName, $"Tutti i turni - {from:MMMM yyyy}");
    }

    /// <summary>
    /// Export Excel dello storico cessioni/scambi (utente loggato).
    /// Query params: year, month (opzionali, default = mese corrente).
    /// </summary>
    [HttpGet("swap-history-excel")]
    public async Task<IActionResult> ExportSwapHistory([FromQuery] int? year, [FromQuery] int? month)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var filterYear = year ?? DateTime.UtcNow.Year;
        var filterMonth = month ?? DateTime.UtcNow.Month;
        var from = new DateTime(filterYear, filterMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        var swaps = await _db.SwapRequests
            .Include(r => r.InitiatorMedico)
            .Include(r => r.CounterpartMedico)
            .Include(r => r.InitiatorShift)
            .Include(r => r.CounterpartShift)
            .Where(r => !r.IsDeleted)
            .Where(r => r.InitiatorMedicoId == userId || r.CounterpartMedicoId == userId)
            .Where(r => r.InitiatorShift.StartUtc >= from && r.InitiatorShift.StartUtc < to)
            .OrderBy(r => r.InitiatorShift.StartUtc)
            .ToListAsync();

        var user = await _db.Users.FindAsync(userId);
        var fileName = $"Scambi_{user?.FirstName}_{user?.LastName}_{filterYear}_{filterMonth:D2}.xlsx";

        return GenerateSwapExcel(swaps, fileName, $"Cessioni-Scambi - {from:MMMM yyyy}");
    }

    private IActionResult GenerateExcel(List<Shift> shifts, string fileName, string sheetName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Header
        worksheet.Cell(1, 1).Value = "Data";
        worksheet.Cell(1, 2).Value = "Giorno settimana";
        worksheet.Cell(1, 3).Value = "Codice turno";
        worksheet.Cell(1, 4).Value = "Inizio (locale)";
        worksheet.Cell(1, 5).Value = "Fine (locale)";
        worksheet.Cell(1, 6).Value = "Medico di turno";
        worksheet.Cell(1, 7).Value = "Medico reperibile";
        worksheet.Cell(1, 8).Value = "Medico esterno";

        var headerRange = worksheet.Range("A1:H1");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Dati
        var row = 2;
        foreach (var s in shifts)
        {
            var date = DateTime.Parse(s.Date.ToString("yyyy-MM-dd"));
            var startLocal = TimeZoneInfo.ConvertTimeFromUtc(s.StartUtc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"));
            var endLocal = TimeZoneInfo.ConvertTimeFromUtc(s.EndUtc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"));

            worksheet.Cell(row, 1).Value = s.Date.ToString("dd/MM/yyyy");
            worksheet.Cell(row, 2).Value = date.ToString("dddd", new System.Globalization.CultureInfo("it-IT"));
            worksheet.Cell(row, 3).Value = s.Code.ToString();
            worksheet.Cell(row, 4).Value = startLocal.ToString("HH:mm");
            worksheet.Cell(row, 5).Value = endLocal.ToString("HH:mm");
            worksheet.Cell(row, 6).Value = s.MedicoTurno != null ? $"{s.MedicoTurno.FirstName} {s.MedicoTurno.LastName}" : "";
            worksheet.Cell(row, 7).Value = s.MedicoReperibile != null ? $"{s.MedicoReperibile.FirstName} {s.MedicoReperibile.LastName}" : "";
            worksheet.Cell(row, 8).Value = s.ExternalDoctor != null ? $"{s.ExternalDoctor.FirstName} {s.ExternalDoctor.LastName}" : "";
            row++;
        }

        // Auto-fit colonne
        worksheet.Columns().AdjustToContents();

        // Genera il file in memoria
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }

    private IActionResult GenerateSwapExcel(List<SwapRequest> swaps, string fileName, string sheetName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        worksheet.Cell(1, 1).Value = "Data turno";
        worksheet.Cell(1, 2).Value = "Codice turno";
        worksheet.Cell(1, 3).Value = "Tipo";
        worksheet.Cell(1, 4).Value = "Stato";
        worksheet.Cell(1, 5).Value = "Iniziatore";
        worksheet.Cell(1, 6).Value = "Controparte";
        worksheet.Cell(1, 7).Value = "Turno controparte";
        worksheet.Cell(1, 8).Value = "Messaggio";
        worksheet.Cell(1, 9).Value = "Motivo risoluzione";
        worksheet.Cell(1, 10).Value = "Creato il";
        worksheet.Cell(1, 11).Value = "Risolto il";

        var headerRange = worksheet.Range("A1:K1");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        var row = 2;
        foreach (var r in swaps)
        {
            worksheet.Cell(row, 1).Value = r.InitiatorShift.Date.ToString("dd/MM/yyyy");
            worksheet.Cell(row, 2).Value = r.InitiatorShift.Code.ToString();
            worksheet.Cell(row, 3).Value = r.Type switch
            {
                SwapRequestType.Giveaway => "Cessione",
                SwapRequestType.Swap => "Scambio",
                SwapRequestType.PickFromBoard => "Presa da bacheca",
                _ => r.Type.ToString()
            };
            worksheet.Cell(row, 4).Value = r.Status switch
            {
                SwapRequestStatus.Pending => "In attesa",
                SwapRequestStatus.AutoApproved => "Approvato",
                SwapRequestStatus.Rejected => "Rifiutato",
                SwapRequestStatus.Cancelled => "Annullato",
                SwapRequestStatus.BlockedByRules => "Bloccato",
                _ => r.Status.ToString()
            };
            worksheet.Cell(row, 5).Value = r.InitiatorMedico != null
                ? $"{r.InitiatorMedico.FirstName} {r.InitiatorMedico.LastName}" : "";
            worksheet.Cell(row, 6).Value = r.CounterpartMedico != null
                ? $"{r.CounterpartMedico.FirstName} {r.CounterpartMedico.LastName}" : "";
            worksheet.Cell(row, 7).Value = r.CounterpartShift != null
                ? $"{r.CounterpartShift.Date:dd/MM/yyyy} {r.CounterpartShift.Code}" : "";
            worksheet.Cell(row, 8).Value = r.Message ?? "";
            worksheet.Cell(row, 9).Value = r.ResolutionReason ?? "";
            worksheet.Cell(row, 10).Value = TimeZoneInfo.ConvertTimeFromUtc(r.CreatedAtUtc, rome).ToString("dd/MM/yyyy HH:mm");
            worksheet.Cell(row, 11).Value = r.ResolvedAtUtc.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(r.ResolvedAtUtc.Value, rome).ToString("dd/MM/yyyy HH:mm") : "";
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }
}
