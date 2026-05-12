using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Application.Common.Services;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Mail;
using OnCallendar.Infrastructure.Persistence;
using OnCallendar.Infrastructure.Persistence.Seed;
using System.Security.Cryptography;
using System.Web;

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
    private readonly IEmailSender _email;
    private readonly MailSettings _mail;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ApplicationDbContext db,
        ICurrentUserService user,
        UserManager<ApplicationUser> users,
        IAuditLogger audit,
        IWebHostEnvironment env,
        IEmailSender email,
        IOptions<MailSettings> mailOpt,
        ILogger<UsersController> logger)
    {
        _db = db; _user = user; _users = users; _audit = audit; _env = env;
        _email = email; _mail = mailOpt.Value; _logger = logger;
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
        bool EmailConfirmed, bool PhoneConfirmed,
        bool IsDefaultEmail, string? PendingEmail,
        bool PasswordChangeRequired, bool PasswordExpired,
        DateTime? PasswordChangedAtUtc);

    private static MeDto Map(ApplicationUser u)
    {
        var changedAt = u.PasswordChangedAtUtc;
        // Se non e` mai stata cambiata (seed/utente di default) -> richiesta forte di cambio.
        var neverChanged = changedAt is null;
        // Scaduta dopo 365 giorni dall'ultimo cambio (o, se mai cambiata, dalla creazione).
        var reference = changedAt ?? u.CreatedAtUtc;
        var expired = reference < DateTime.UtcNow.AddYears(-1);
        return new MeDto(
            u.Id, u.Email!, u.FirstName, u.LastName,
            u.PhoneNumber, u.AvatarUrl,
            u.PreferredLanguage ?? "it",
            u.ThemePreference ?? "system",
            u.EmailConfirmed, u.PhoneNumberConfirmed,
            u.IsDefaultEmail, u.PendingEmail,
            PasswordChangeRequired: neverChanged,
            PasswordExpired: expired,
            PasswordChangedAtUtc: u.PasswordChangedAtUtc);
    }

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
        // Il cambio email NON è più gestito qui: passa per /me/request-email-change
        // con conferma via link inviato al nuovo indirizzo.
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
        if (req is null || string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { error = "Dati mancanti." });
        if (string.Equals(req.CurrentPassword, req.NewPassword, StringComparison.Ordinal))
            return BadRequest(new { errors = new[] { "La nuova password deve essere diversa da quella attuale." } });
        var u = await _users.FindByIdAsync(uid.ToString());
        if (u is null) return NotFound();
        var res = await _users.ChangePasswordAsync(u, req.CurrentPassword, req.NewPassword);
        if (!res.Succeeded)
            return BadRequest(new { errors = res.Errors.Select(e => e.Description) });
        u.PasswordChangedAtUtc = DateTime.UtcNow;
        await _users.UpdateAsync(u);
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

    public sealed record RequestEmailChangeRequest(string NewEmail, string? ClientCallbackUrl = null);

    /// <summary>
    /// Richiede il cambio email: invia un link di conferma al NUOVO indirizzo.
    /// L'email diventa effettiva solo quando l'utente conferma cliccando il link.
    /// Finch&#233; non confermata, <c>PendingEmail</c> &#232; valorizzata e l'app mostra
    /// la notifica fissa "email da confermare".
    /// </summary>
    [HttpPost("me/request-email-change")]
    public async Task<IActionResult> RequestEmailChange([FromBody] RequestEmailChangeRequest req)
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        if (req is null || string.IsNullOrWhiteSpace(req.NewEmail))
            return BadRequest(new { error = "Email obbligatoria." });
        var newEmail = req.NewEmail.Trim();
        if (!newEmail.Contains('@'))
            return BadRequest(new { error = "Email non valida." });

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == uid);
        if (u is null) return NotFound();

        if (string.Equals(u.Email, newEmail, StringComparison.OrdinalIgnoreCase) && u.EmailConfirmed && !u.IsDefaultEmail)
            return BadRequest(new { error = "L'email indicata coincide con quella attuale gia` confermata." });

        // Email gia` in uso da un altro utente?
        var clash = await _db.Users.FirstOrDefaultAsync(x =>
            x.Id != uid && x.Email != null && x.Email.ToLower() == newEmail.ToLower());
        if (clash is not null)
            return Conflict(new { error = "Email gia` registrata per un altro utente." });

        u.PendingEmail = newEmail;
        u.EmailChangeToken = GenerateUrlSafeToken(32);
        u.EmailChangeRequestedAtUtc = DateTime.UtcNow;
        u.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var baseUrl = EmailTemplates.ResolveCallbackBaseUrl(req?.ClientCallbackUrl, _mail);
        var url = $"{baseUrl}/confirm-email?token={u.EmailChangeToken}";
        // Wrap https per renderlo cliccabile in qualsiasi client mail (Gmail/Outlook
        // bloccano scheme custom come exp:// o oncallendar://). La pagina /r/go
        // del backend fa il redirect immediato all'app reale.
        // Risolto via PublicRedirectBaseUrl => Railway anche se siamo in DEV.
        var publicBackend = EmailTemplates.ResolvePublicRedirectBaseUrl(_mail, $"{Request.Scheme}://{Request.Host.Value}");
        // Per conferma email usiamo SEMPRE l'endpoint server-side /r/confirm-email
        // che conferma direttamente nel browser. Questo evita problemi con i deep
        // link temporanei (Expo tunnel, scheme custom non cliccabili, ecc.).
        var ctaUrl = $"{publicBackend}/r/confirm-email?token={u.EmailChangeToken}";

        var html = EmailTemplates.Build(
            preheader: $"Conferma il nuovo indirizzo email {newEmail}.",
            title: "Conferma il nuovo indirizzo email",
            greetingName: u.FirstName,
            paragraphs: new[]
            {
                $"Hai richiesto di impostare <b>{HttpUtility.HtmlEncode(newEmail)}</b> come tua email su OnCallendar.",
                "Per attivare il nuovo indirizzo conferma cliccando il bottone qui sotto. Il link &egrave; valido per <b>7 giorni</b>.",
            },
            ctaLabel: "Conferma email",
            ctaUrl: ctaUrl,
            footerNote: "Se non hai richiesto tu il cambio puoi ignorare questa email: il tuo account non subir&agrave; modifiche.");
        var text = $"Conferma il cambio email aprendo questo link (valido 7 giorni):\n{ctaUrl}\n\nSe non hai richiesto tu il cambio, ignora questa email.";
        try
        {
            await _email.SendAsync(
                toEmail: newEmail,
                toName: $"{u.FirstName} {u.LastName}",
                subject: "Conferma il tuo nuovo indirizzo email",
                htmlBody: html,
                plainBody: text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Users/RequestEmailChange] Errore invio mail a {Email}", newEmail);
            return StatusCode(500, new { error = "Impossibile inviare l'email di conferma." });
        }

        _audit.Log("ApplicationUser", u.Id, "EmailChangeRequested", u.TenantId ?? Guid.Empty);
        return Ok(new { ok = true, pendingEmail = newEmail });
    }

    private static string GenerateUrlSafeToken(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// DEV ONLY: imposta la PasswordChangedAtUtc dell'utente corrente a 366 giorni fa,
    /// in modo che al prossimo login il backend ritorni passwordExpired=true.
    /// Aperto a tutti gli utenti autenticati: serve allo sviluppatore per testare
    /// la notifica di scadenza password indipendentemente dal ruolo loggato.
    /// </summary>
    [HttpPost("me/dev-expire-password")]
    [Authorize]
    public async Task<IActionResult> DevExpirePassword()
    {
        if (_user.UserId is not Guid uid) return Unauthorized();
        var u = await _users.FindByIdAsync(uid.ToString());
        if (u is null) return NotFound();
        u.PasswordChangedAtUtc = DateTime.UtcNow.AddDays(-366);
        await _users.UpdateAsync(u);
        _audit.Log("ApplicationUser", u.Id, "DevExpiredPassword", u.TenantId ?? Guid.Empty);
        return Ok(new { ok = true, passwordChangedAtUtc = u.PasswordChangedAtUtc });
    }

    /// <summary>
    /// DEV ONLY: conferma la pending email dell'utente corrente senza passare
    /// dall'email. Utile quando si testa con Expo Go (tunnel mode) dove il link
    /// nell'email non è raggiungibile perché punta al backend di produzione ma
    /// il token è nel database locale.
    /// </summary>
    [HttpPost("me/dev-confirm-email")]
    [Authorize]
    public async Task<IActionResult> DevConfirmEmail()
    {
        if (!_env.IsDevelopment())
            return NotFound();

        if (_user.UserId is not Guid uid) return Unauthorized();
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == uid);
        if (u is null) return NotFound();
        if (u.PendingEmail is null)
            return BadRequest(new { error = "Nessun cambio email in sospeso." });

        var pending = u.PendingEmail!;
        var clash = await _db.Users.FirstOrDefaultAsync(x =>
            x.Id != u.Id && x.Email != null && x.Email.ToLower() == pending.ToLower());
        if (clash is not null)
        {
            u.PendingEmail = null;
            u.EmailChangeToken = null;
            u.EmailChangeRequestedAtUtc = null;
            await _db.SaveChangesAsync();
            return Conflict(new { error = "Email già registrata per un altro utente." });
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

        _audit.Log("ApplicationUser", u.Id, "DevConfirmedEmail", u.TenantId ?? Guid.Empty);
        return Ok(new { ok = true, email = pending });
    }
}
