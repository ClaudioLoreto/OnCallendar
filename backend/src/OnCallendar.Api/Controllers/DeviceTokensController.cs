using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Api.Contracts;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Entities;

namespace OnCallendar.Api.Controllers;

/// <summary>
/// Registrazione/deregistrazione dei device push (Expo) per l'utente corrente.
/// Il token è un valore Expo nella forma "ExponentPushToken[xxxxxxxxxxxxxxxxxxxxxx]"
/// fornito dal client tramite <c>Notifications.getExpoPushTokenAsync()</c>.
/// </summary>
[ApiController]
[Route("api/device-tokens")]
[Authorize]
public sealed class DeviceTokensController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public DeviceTokensController(IApplicationDbContext db, ICurrentUserService user)
    {
        _db = db; _user = user;
    }

    /// <summary>
    /// Upsert del token: se esiste già per questo utente lo riattiva e aggiorna
    /// platform/deviceName/lastSeen, altrimenti lo crea.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Token))
            return BadRequest(new { error = "Token vuoto." });

        var tenantId = _user.TenantId ?? Guid.Empty;
        var token = req.Token.Trim();
        var platform = string.IsNullOrWhiteSpace(req.Platform) ? "unknown" : req.Platform.Trim().ToLowerInvariant();
        if (platform.Length > 16) platform = platform[..16];

        var existing = await _db.UserDeviceTokens
            .FirstOrDefaultAsync(d => d.UserId == uid && d.Token == token);

        if (existing is null)
        {
            _db.UserDeviceTokens.Add(new UserDeviceToken
            {
                TenantId = tenantId,
                UserId = uid,
                Token = token,
                Platform = platform,
                DeviceName = req.DeviceName,
                IsActive = true,
                LastSeenUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            existing.IsActive = true;
            existing.Platform = platform;
            existing.DeviceName = req.DeviceName ?? existing.DeviceName;
            existing.LastSeenUtc = DateTime.UtcNow;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Disattiva (soft-delete) un token al logout.</summary>
    [HttpDelete("{token}")]
    public async Task<IActionResult> Unregister(string token)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var existing = await _db.UserDeviceTokens
            .FirstOrDefaultAsync(d => d.UserId == uid && d.Token == token);
        if (existing is null) return NotFound();
        existing.IsActive = false;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
