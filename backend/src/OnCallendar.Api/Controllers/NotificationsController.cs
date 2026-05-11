using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Notifications;
using OnCallendar.Infrastructure.Persistence;

namespace OnCallendar.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public NotificationsController(ApplicationDbContext db, ICurrentUserService user)
    {
        _db = db; _user = user;
    }

    public sealed record NotificationDto(
        Guid Id, string Type, string? Title, string Message, string? Category,
        bool IsRead, Guid? RelatedEntityId, string? DataJson, DateTime CreatedAtUtc);

    /// <summary>Ultime 50 notifiche dell'utente (non lette prima).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> List()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var items = await _db.Notifications
            .Where(n => n.UserId == uid)
            .OrderBy(n => n.IsRead)
            .ThenByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.Title, n.Message, n.Category,
                n.IsRead, n.RelatedEntityId, n.DataJson, n.CreatedAtUtc))
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Conta le notifiche non lette. Usato per il badge campanella.</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult> UnreadCount()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var count = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead);
        return Ok(new { count });
    }

    /// <summary>Segna una singola notifica come letta.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        if (n is null) return NotFound();
        n.IsRead = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Segna TUTTE le notifiche dell'utente come lette.</summary>
    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        await _db.Notifications
            .Where(n => n.UserId == uid && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return NoContent();
    }

    // ----------------------------------------------------------------------
    // PREFERENZE: matrice tipo × canale. Default = abilitato; salviamo solo
    // l'override esplicito in NotificationPreferences.
    // ----------------------------------------------------------------------

    public sealed record PreferenceDto(string Type, string Channel, bool Enabled);

    [HttpGet("preferences")]
    public async Task<ActionResult<IEnumerable<PreferenceDto>>> GetPreferences()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        var stored = await _db.NotificationPreferences
            .Where(p => p.UserId == uid)
            .ToListAsync();

        var result = new List<PreferenceDto>(NotificationTypeCodes.All.Count * NotificationChannels.All.Count);
        foreach (var type in NotificationTypeCodes.All)
        {
            foreach (var channel in NotificationChannels.All)
            {
                var ovr = stored.FirstOrDefault(p =>
                    string.Equals(p.Type, type, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.Channel, channel, StringComparison.OrdinalIgnoreCase));
                result.Add(new PreferenceDto(type, channel, ovr?.Enabled ?? true));
            }
        }
        return Ok(result);
    }

    public sealed record SetPreferenceRequest(string Type, string Channel, bool Enabled);

    [HttpPut("preferences")]
    public async Task<IActionResult> SetPreference([FromBody] SetPreferenceRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();

        if (!NotificationTypeCodes.All.Contains(req.Type, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Tipo notifica non valido." });
        if (!NotificationChannels.All.Contains(req.Channel, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Canale non valido." });

        var tenantId = _user.TenantId ?? Guid.Empty;
        var existing = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == uid &&
                                      p.Type == req.Type &&
                                      p.Channel == req.Channel);

        if (existing is null)
        {
            _db.NotificationPreferences.Add(new NotificationPreference
            {
                TenantId = tenantId,
                UserId = uid,
                Type = req.Type,
                Channel = req.Channel,
                Enabled = req.Enabled,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Enabled = req.Enabled;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
