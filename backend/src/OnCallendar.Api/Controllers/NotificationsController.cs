using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
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
        Guid Id, string Type, string Message, bool IsRead,
        Guid? RelatedEntityId, DateTime CreatedAtUtc);

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
            .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.IsRead, n.RelatedEntityId, n.CreatedAtUtc))
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
}
