using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnCallendar.Api.Auth;
using OnCallendar.Api.Contracts;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Common;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Mail;
using System.Text;
using System.Web;

namespace OnCallendar.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IJwtTokenService _jwt;
    private readonly IApplicationDbContext _db;
    private readonly IEmailSender _email;
    private readonly MailSettings _mail;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IJwtTokenService jwt,
        IApplicationDbContext db,
        IEmailSender email,
        IOptions<MailSettings> mailOpt,
        IWebHostEnvironment env,
        ILogger<AuthController> logger)
    {
        _users = users;
        _signIn = signIn;
        _jwt = jwt;
        _db = db;
        _email = email;
        _mail = mailOpt.Value;
        _env = env;
        _logger = logger;
    }

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

        // Scadenza password annuale: se mai cambiata o cambiata > 365 giorni fa.
        var passwordExpired = (user.PasswordChangedAtUtc ?? user.CreatedAtUtc) < DateTime.UtcNow.AddYears(-1);

        return Ok(new LoginResponse(
            token, exp, user.Id, user.Email!, $"{user.FirstName} {user.LastName}",
            user.Role.ToString(), user.TenantId, passwordExpired));
    }

    /// <summary>
    /// Registrazione di un nuovo medico — riservata al SuperAdmin.
    /// Non esiste auto-registrazione pubblica.
    /// </summary>
    [HttpPost("register-medico")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
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

        await _users.AddToRoleAsync(user, RoleNames.Medico);

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

    /// <summary>
    /// Avvia la procedura di reset password: genera un token Identity (1 ora di validita`)
    /// e invia per email il link di reset. NON nasconde l'esistenza dell'email: se
    /// l'indirizzo non corrisponde a un account attivo, restituisce 404 esplicito
    /// (richiesto dal product owner per evitare invii a indirizzi sbagliati).
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        var email = (req?.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "Email obbligatoria." });

        var user = await _users.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
            return NotFound(new { error = "Nessun account associato a questa email." });

        try
        {
            var rawToken = await _users.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawToken));
            var encodedEmail = HttpUtility.UrlEncode(email);

            var baseUrl = EmailTemplates.ResolveCallbackBaseUrl(req?.ClientCallbackUrl, _mail);
            var resetUrl = $"{baseUrl}/reset-password?email={encodedEmail}&token={encodedToken}";
            // Wrap https per email-client compatibility (vedi RedirectController).
            // Usa PublicRedirectBaseUrl (Railway) se configurato, cosi' il bottone
            // funziona anche in DEV dove il backend gira su localhost.
            var publicBackend = EmailTemplates.ResolvePublicRedirectBaseUrl(_mail, $"{Request.Scheme}://{Request.Host.Value}");
            var ctaUrl = EmailTemplates.WrapForEmail(resetUrl, publicBackend);

            var html = EmailTemplates.Build(
                preheader: "Reimposta la tua password OnCallendar.",
                title: "Reimposta la password",
                greetingName: user.FirstName,
                paragraphs: new[]
                {
                    "Hai richiesto di reimpostare la password del tuo account OnCallendar.",
                    "Clicca il bottone qui sotto per scegliere una nuova password. Il link &egrave; valido per <b>1 ora</b>.",
                },
                ctaLabel: "Reimposta password",
                ctaUrl: ctaUrl,
                footerNote: "Se non hai richiesto tu il reset puoi ignorare questa email: la tua password attuale resta valida.");
            var text = $"Reimposta la password aprendo questo link (valido 1 ora):\n{ctaUrl}\n\nSe non hai richiesto tu il reset, ignora questa email.";

            await _email.SendAsync(
                toEmail: email,
                toName: $"{user.FirstName} {user.LastName}".Trim(),
                subject: "Reimposta la tua password",
                htmlBody: html,
                plainBody: text,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Auth/ForgotPassword] Errore invio email reset a {Email}", email);
            return StatusCode(500, new { error = "Impossibile inviare l'email di reset." });
        }

        return Ok(new { ok = true });
    }

    /// <summary>
    /// Completa il reset password. Riceve email + token (base64) + nuova password.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { error = "Dati mancanti." });

        var user = await _users.FindByEmailAsync(req.Email.Trim());
        if (user is null || !user.IsActive)
            return BadRequest(new { error = "Link non valido o scaduto." });

        string rawToken;
        try { rawToken = Encoding.UTF8.GetString(Convert.FromBase64String(req.Token)); }
        catch { return BadRequest(new { error = "Link non valido o scaduto." }); }

        // Vieta nuova password identica a quella attuale.
        if (await _users.CheckPasswordAsync(user, req.NewPassword))
            return BadRequest(new { error = "La nuova password deve essere diversa da quella attuale." });

        var res = await _users.ResetPasswordAsync(user, rawToken, req.NewPassword);
        if (!res.Succeeded)
            return BadRequest(new { error = string.Join(" ", res.Errors.Select(e => e.Description)) });

        user.PasswordChangedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[Auth/ResetPassword] Password reimpostata per {Email}", req.Email);
        return Ok(new { ok = true });
    }

    public sealed record ExternalInviteInfoDto(string FirstName, string LastName, string? Email);

    /// <summary>
    /// Restituisce le info pre-popolate (nome/cognome) per un invito di registrazione
    /// medico esterno, dato il token. Pubblica.
    /// </summary>
    [HttpGet("external-invite/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<ExternalInviteInfoDto>> GetExternalInvite(string token)
    {
        var ext = await _db.ExternalDoctors.FirstOrDefaultAsync(e => e.InviteToken == token);
        if (ext is null) return NotFound(new { error = "Invito non valido o scaduto." });
        if (ext.RegisteredAtUtc is not null)
            return BadRequest(new { error = "Questo invito è già stato utilizzato." });
        return Ok(new ExternalInviteInfoDto(ext.FirstName, ext.LastName, ext.Email));
    }

    /// <summary>
    /// Completa la registrazione di un medico esterno tramite token di invito.
    /// Crea l'utente Identity (ruolo Medico, tenant del medico che lo aveva invitato),
    /// collega il record ExternalDoctor e disattiva il token (no più email).
    /// </summary>
    [HttpPost("register-external")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterExternal([FromBody] RegisterExternalRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Dati mancanti." });

        var ext = await _db.ExternalDoctors.FirstOrDefaultAsync(e => e.InviteToken == req.Token);
        if (ext is null) return BadRequest(new { error = "Invito non valido o scaduto." });
        if (ext.RegisteredAtUtc is not null)
            return BadRequest(new { error = "Questo invito è già stato utilizzato." });

        var email = req.Email.Trim();
        if (await _users.FindByEmailAsync(email) is not null)
            return Conflict(new { error = "Email già registrata. Effettua il login." });

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = ext.FirstName,
            LastName = ext.LastName,
            Phone = ext.Phone,
            Role = UserRole.Medico,
            TenantId = ext.TenantId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            PasswordChangedAtUtc = DateTime.UtcNow,
        };

        var res = await _users.CreateAsync(user, req.Password);
        if (!res.Succeeded)
            return BadRequest(new { error = string.Join(" ", res.Errors.Select(e => e.Description)) });

        await _users.AddToRoleAsync(user, RoleNames.Medico);

        // Collega il medico esterno e disattiva il token: niente più email di invito.
        ext.LinkedUserId = user.Id;
        ext.RegisteredAtUtc = DateTime.UtcNow;
        ext.InviteToken = null;
        ext.Email = email;
        ext.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[Auth/RegisterExternal] Medico esterno registrato: {Email}", email);
        return Ok(new { ok = true });
    }

    /// <summary>
    /// Conferma un cambio email precedentemente richiesto via /api/users/me/request-email-change.
    /// Promuove PendingEmail a Email ufficiale e marca EmailConfirmed = true,
    /// IsDefaultEmail = false, azzera i campi temporanei.
    /// </summary>
    [HttpPost("confirm-email-change")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmailChange([FromBody] ConfirmEmailChangeRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Token))
            return BadRequest(new { error = "Token mancante." });

        var u = await _db.Users.FirstOrDefaultAsync(x => x.EmailChangeToken == req.Token);
        if (u is null) return BadRequest(new { error = "Link non valido o scaduto." });
        if (u.PendingEmail is null) return BadRequest(new { error = "Nessun cambio email in corso." });
        if (u.EmailChangeRequestedAtUtc is { } req0 && req0 < DateTime.UtcNow.AddDays(-7))
            return BadRequest(new { error = "Link scaduto. Richiedi un nuovo cambio email." });

        var pending = u.PendingEmail!;
        var clash = await _db.Users.FirstOrDefaultAsync(x =>
            x.Id != u.Id && x.Email != null && x.Email.ToLower() == pending.ToLower());
        if (clash is not null)
        {
            u.PendingEmail = null;
            u.EmailChangeToken = null;
            u.EmailChangeRequestedAtUtc = null;
            await _db.SaveChangesAsync();
            return Conflict(new { error = "Email gia` registrata per un altro utente." });
        }

        var setEmail = await _users.SetEmailAsync(u, pending);
        if (!setEmail.Succeeded)
            return BadRequest(new { error = string.Join(", ", setEmail.Errors.Select(e => e.Description)) });
        var setUser = await _users.SetUserNameAsync(u, pending);
        if (!setUser.Succeeded)
            return BadRequest(new { error = string.Join(", ", setUser.Errors.Select(e => e.Description)) });

        u.EmailConfirmed = true;
        u.IsDefaultEmail = false;
        u.PendingEmail = null;
        u.EmailChangeToken = null;
        u.EmailChangeRequestedAtUtc = null;
        u.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[Auth/ConfirmEmailChange] Email confermata per utente {UserId}: {Email}", u.Id, pending);
        return Ok(new { ok = true, email = pending });
    }

    /// <summary>
    /// DEV ONLY: resetta la password di un utente senza token email.
    /// Utile per testare il flusso "forgot password" da Expo Go dove il link
    /// nell'email punta al backend di produzione (che ha un DB diverso).
    /// </summary>
    [HttpPost("dev-reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> DevResetPassword([FromBody] DevResetPasswordRequest req)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        if (req is null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { error = "Email e nuova password obbligatorie." });

        var user = await _users.FindByEmailAsync(req.Email.Trim());
        if (user is null || !user.IsActive)
            return NotFound(new { error = "Nessun account associato a questa email." });

        // Vieta nuova password identica a quella attuale.
        if (await _users.CheckPasswordAsync(user, req.NewPassword))
            return BadRequest(new { error = "La nuova password deve essere diversa da quella attuale." });

        var rawToken = await _users.GeneratePasswordResetTokenAsync(user);
        var res = await _users.ResetPasswordAsync(user, rawToken, req.NewPassword);
        if (!res.Succeeded)
            return BadRequest(new { error = string.Join(" ", res.Errors.Select(e => e.Description)) });

        user.PasswordChangedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[Auth/DevResetPassword] Password reimpostata (DEV) per {Email}", req.Email);
        return Ok(new { ok = true });
    }
}
