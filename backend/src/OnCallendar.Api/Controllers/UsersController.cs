using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Application.Common.Services;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Persistence;

namespace OnCallendar.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditLogger _audit;
    private readonly IWebHostEnvironment _env;

    public UsersController(
        ApplicationDbContext db,
        ICurrentUserService user,
        UserManager<ApplicationUser> users,
        IAuditLogger audit,
        IWebHostEnvironment env)
    {
        _db = db; _user = user; _users = users; _audit = audit; _env = env;
    }

    public sealed record MedicoDto(Guid Id, string FullName, string Email, string? AvatarUrl);

    [HttpGet("medici")]
    public async Task<ActionResult<IEnumerable<MedicoDto>>> Medici()
    {
        var list = await _db.Users
            .Where(u => u.Role == UserRole.Medico && u.IsActive)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new MedicoDto(u.Id, u.FirstName + " " + u.LastName, u.Email!, u.AvatarUrl))
            .ToListAsync();
        return Ok(list);
    }

    public sealed record MeDto(
        Guid Id, string Email, string FirstName, string LastName,
        string? Phone, string? AvatarUrl,
        string PreferredLanguage, string ThemePreference,
        bool EmailConfirmed, bool PhoneConfirmed);

    private static MeDto Map(ApplicationUser u) => new(
        u.Id, u.Email!, u.FirstName, u.LastName,
        u.PhoneNumber, u.AvatarUrl,
        u.PreferredLanguage ?? "it",
        u.ThemePreference ?? "system",
        u.EmailConfirmed, u.PhoneNumberConfirmed);

    [HttpGet("me")]
    public async Task<ActionResult<MeDto>> Me()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == uid);
        if (u is null) return NotFound();
        return Ok(Map(u));
    }

    public sealed record UpdateMeRequest(
        string? FirstName, string? LastName,
        string? Email, string? Phone, string? AvatarUrl,
        string? PreferredLanguage, string? ThemePreference);

    [HttpPatch("me")]
    public async Task<ActionResult<MeDto>> UpdateMe([FromBody] UpdateMeRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == uid);
        if (u is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.FirstName)) u.FirstName = req.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(req.LastName))  u.LastName  = req.LastName.Trim();
        if (!string.IsNullOrWhiteSpace(req.Email) && !string.Equals(req.Email.Trim(), u.Email, StringComparison.OrdinalIgnoreCase))
        {
            var newEmail = req.Email.Trim();
            var setEmail = await _users.SetEmailAsync(u, newEmail);
            if (!setEmail.Succeeded)
                return BadRequest(new { error = string.Join(", ", setEmail.Errors.Select(e => e.Description)) });
            var setUser = await _users.SetUserNameAsync(u, newEmail);
            if (!setUser.Succeeded)
                return BadRequest(new { error = string.Join(", ", setUser.Errors.Select(e => e.Description)) });
            u.EmailConfirmed = false;
        }
        if (req.Phone is not null)
        {
            u.PhoneNumber = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
            u.PhoneNumberConfirmed = false;
        }
        if (req.AvatarUrl is not null)
            u.AvatarUrl = string.IsNullOrWhiteSpace(req.AvatarUrl) ? null : req.AvatarUrl.Trim();
        if (!string.IsNullOrWhiteSpace(req.PreferredLanguage))
            u.PreferredLanguage = req.PreferredLanguage.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(req.ThemePreference))
            u.ThemePreference = req.ThemePreference.Trim().ToLowerInvariant();

        u.UpdatedAtUtc = DateTime.UtcNow;
        _audit.Log("ApplicationUser", u.Id, "ProfileUpdated", u.TenantId ?? Guid.Empty);
        await _db.SaveChangesAsync();
        return Ok(Map(u));
    }

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var u = await _users.FindByIdAsync(uid.ToString());
        if (u is null) return NotFound();
        var res = await _users.ChangePasswordAsync(u, req.CurrentPassword, req.NewPassword);
        if (!res.Succeeded)
            return BadRequest(new { errors = res.Errors.Select(e => e.Description) });
        _audit.Log("ApplicationUser", u.Id, "PasswordChanged", u.TenantId ?? Guid.Empty);
        return NoContent();
    }

    [HttpPost("me/avatar")]
    [RequestSizeLimit(8_000_000)]
    public async Task<ActionResult<MeDto>> UploadAvatar(IFormFile file)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new { error = "File mancante." });
        if (file.Length > 5_000_000) return BadRequest(new { error = "Immagine troppo grande (max 5MB)." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
            return BadRequest(new { error = "Formato non supportato (jpg, png, webp)." });

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == uid);
        if (u is null) return NotFound();

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", "avatars");
        Directory.CreateDirectory(dir);
        var fileName = $"{uid}_{DateTime.UtcNow.Ticks}{ext}";
        var fullPath = Path.Combine(dir, fileName);
        await using (var fs = System.IO.File.Create(fullPath)) { await file.CopyToAsync(fs); }

        u.AvatarUrl = $"/uploads/avatars/{fileName}";
        u.UpdatedAtUtc = DateTime.UtcNow;
        _audit.Log("ApplicationUser", u.Id, "AvatarUploaded", u.TenantId ?? Guid.Empty);
        await _db.SaveChangesAsync();
        return Ok(Map(u));
    }
}
