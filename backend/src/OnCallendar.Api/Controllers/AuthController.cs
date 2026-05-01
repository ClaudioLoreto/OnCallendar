using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Api.Auth;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Persistence;
using OnCallendar.Infrastructure.Persistence.Seed;

namespace OnCallendar.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IJwtTokenService _jwt;
    private readonly ApplicationDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IJwtTokenService jwt,
        ApplicationDbContext db)
    {
        _users = users;
        _signIn = signIn;
        _jwt = jwt;
        _db = db;
    }

    public sealed record LoginRequest(string Email, string Password);
    public sealed record LoginResponse(
        string Token,
        DateTime ExpiresAtUtc,
        Guid UserId,
        string Email,
        string FullName,
        string Role,
        Guid? TenantId);

    /// <summary>
    /// Login pubblico (medici e SuperAdmin).
    /// Il campo "Email" accetta indifferentemente l'email completa o il
    /// codice badge (es. "M01").
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var identifier = (req.Email ?? string.Empty).Trim();
        if (identifier.Length == 0)
            return Unauthorized(new { error = "Credenziali non valide." });

        ApplicationUser? user = null;

        if (identifier.Contains('@'))
        {
            user = await _users.FindByEmailAsync(identifier);
        }
        else
        {
            // Badge case-insensitive
            var badge = identifier.ToUpperInvariant();
            user = await _db.Users.FirstOrDefaultAsync(u => u.Badge == badge);
        }

        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "Credenziali non valide." });

        var ok = await _users.CheckPasswordAsync(user, req.Password);
        if (!ok) return Unauthorized(new { error = "Credenziali non valide." });

        var roles = await _users.GetRolesAsync(user);
        var (token, exp) = _jwt.Create(user, roles);

        return Ok(new LoginResponse(
            token, exp, user.Id, user.Email!, $"{user.FirstName} {user.LastName}",
            user.Role.ToString(), user.TenantId));
    }

    public sealed record RegisterMedicoRequest(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        Guid TenantId,
        string? FiscalCode = null,
        string? MedicalRegistrationNumber = null);

    /// <summary>
    /// Registrazione di un nuovo medico — riservata al SuperAdmin.
    /// Non esiste auto-registrazione pubblica.
    /// </summary>
    [HttpPost("register-medico")]
    [Authorize(Roles = DbSeeder.SuperAdminRole)]
    public async Task<IActionResult> RegisterMedico([FromBody] RegisterMedicoRequest req)
    {
        var tenant = await _db.Tenants.FindAsync(req.TenantId);
        if (tenant is null) return BadRequest(new { error = "Tenant inesistente." });

        if (await _users.FindByEmailAsync(req.Email) is not null)
            return Conflict(new { error = "Email già registrata." });

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            EmailConfirmed = true,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Role = UserRole.Medico,
            TenantId = req.TenantId,
            FiscalCode = req.FiscalCode,
            MedicalRegistrationNumber = req.MedicalRegistrationNumber,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var res = await _users.CreateAsync(user, req.Password);
        if (!res.Succeeded)
            return BadRequest(new { errors = res.Errors.Select(e => e.Description) });

        await _users.AddToRoleAsync(user, DbSeeder.MedicoRole);

        return Created($"/api/users/{user.Id}", new
        {
            user.Id, user.Email, user.FirstName, user.LastName, user.TenantId
        });
    }

    /// <summary>Profilo dell'utente loggato (verifica token).</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var roles = await _users.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.TenantId,
            Role = user.Role.ToString(),
            Roles = roles
        });
    }

    public sealed record ForgotPasswordRequest(string EmailOrPhone);

    /// <summary>
    /// Avvia la procedura di reset password via OTP (email/SMS).
    /// Stub di sviluppo: ritorna 501 finché il provider OTP non è configurato.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        // TODO(prod): generare OTP, salvarlo hashato + scadenza, inviare via SMTP/SMS.
        return StatusCode(501, new
        {
            error = "Procedura OTP non ancora attiva in questa build.",
            hint = "Contatta l'amministratore per il reset password."
        });
    }

    public sealed record VerifyOtpRequest(string EmailOrPhone, string Otp, string NewPassword);

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public IActionResult VerifyOtp([FromBody] VerifyOtpRequest req)
    {
        return StatusCode(501, new
        {
            error = "Verifica OTP non ancora attiva in questa build.",
        });
    }
}
