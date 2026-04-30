using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;

namespace OnCallendar.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed di sviluppo: crea il tenant "L'Aquila", un SuperAdmin globale
/// e 3 medici operativi. Idempotente: se già esistono, non duplica.
/// </summary>
public static class DbSeeder
{
    public const string SuperAdminRole = "SuperAdmin";
    public const string MedicoRole     = "Medico";

    // Credenziali di test (DEV ONLY — cambiare in produzione!)
    public const string AdminEmail    = "admin@oncallendar.local";
    public const string AdminPassword = "Admin#2026!";

    public static readonly (string Email, string Password, string First, string Last)[] Medici =
    {
        ("mario.rossi@aquila.med",    "Medico#2026!", "Mario",  "Rossi"),
        ("giulia.bianchi@aquila.med", "Medico#2026!", "Giulia", "Bianchi"),
        ("luca.verdi@aquila.med",     "Medico#2026!", "Luca",   "Verdi"),
    };

    public const string AquilaTenantSlug = "laquila";

    public static async Task SeedAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        // ---- Roles ----
        foreach (var role in new[] { SuperAdminRole, MedicoRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        // ---- Tenant L'Aquila ----
        // IgnoreQueryFilters per essere indipendenti dal TenantProvider durante il seed.
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == AquilaTenantSlug, ct);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = "Guardia Medica L'Aquila",
                Slug = AquilaTenantSlug,
                Address = "Via dell'Ospedale, L'Aquila (AQ)",
                TimeZoneId = "Europe/Rome",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);
        }

        // ---- SuperAdmin globale (TenantId = null) ----
        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                FirstName = "Super",
                LastName = "Admin",
                Role = UserRole.SuperAdmin,
                TenantId = null,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            };
            var res = await userManager.CreateAsync(admin, AdminPassword);
            if (!res.Succeeded)
                throw new InvalidOperationException(
                    "Seed admin failed: " + string.Join("; ", res.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(admin, SuperAdminRole);
        }

        // ---- Medici del tenant L'Aquila ----
        foreach (var (email, password, first, last) in Medici)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null) continue;

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = first,
                LastName = last,
                Role = UserRole.Medico,
                TenantId = tenant.Id,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            };
            var res = await userManager.CreateAsync(user, password);
            if (!res.Succeeded)
                throw new InvalidOperationException(
                    $"Seed medico {email} failed: " +
                    string.Join("; ", res.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, MedicoRole);
        }

        await SeedShiftsAsync(db, tenant.Id, ct);
    }

    /// <summary>
    /// Crea per ogni giorno (oggi → +14gg) due turni da 12h (00:00–12:00 e 12:00–24:00 ora locale)
    /// con capacità 2. I turni vengono pre-popolati lasciando volutamente
    /// dei buchi così che Mario possa prenotarsi in fase di demo.
    /// Idempotente: se trova già la struttura "v2" non rifà nulla.
    /// </summary>
    private static async Task SeedShiftsAsync(
        ApplicationDbContext db, Guid tenantId, CancellationToken ct)
    {
        const string SeedMarker = "SEED_V2_DAILY_2x12H";

        // Se esiste già almeno un turno con il marker corrente => già seedato.
        var alreadySeeded = await db.Shifts.IgnoreQueryFilters()
            .AnyAsync(s => s.TenantId == tenantId && s.Notes != null && s.Notes.Contains(SeedMarker), ct);
        if (alreadySeeded) return;

        // Wipe vecchi dati turni (dev only, mai in produzione).
        // Cancello in ordine: SwapRequests -> Assignments -> Shifts (FK safe).
        var oldSwaps = await db.SwapRequests.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId).ToListAsync(ct);
        if (oldSwaps.Count > 0) db.SwapRequests.RemoveRange(oldSwaps);

        var oldAssigns = await db.ShiftAssignments.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId).ToListAsync(ct);
        if (oldAssigns.Count > 0) db.ShiftAssignments.RemoveRange(oldAssigns);

        var oldShifts = await db.Shifts.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId).ToListAsync(ct);
        if (oldShifts.Count > 0) db.Shifts.RemoveRange(oldShifts);

        await db.SaveChangesAsync(ct);

        var medici = await db.Users.IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && u.Role == UserRole.Medico)
            .OrderBy(u => u.LastName)
            .ToListAsync(ct);
        if (medici.Count < 3) return;

        var rome = TryGetRomeTz();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, rome);
        var startDayLocal = nowLocal.Date; // oggi 00:00 locale

        // Pattern di pre-popolamento: per ogni (giorno, slot) decido quanti medici inserire (0/1/2)
        // così che ci siano sia turni vuoti, sia turni a metà, sia turni pieni.
        // Slot 0 = 00-12, Slot 1 = 12-24
        // Lasciamo OGGI praticamente vuoto, mentre i giorni successivi hanno carico crescente.
        var rnd = new Random(42); // deterministico
        var rotation = 0;

        for (int day = 0; day < 14; day++)
        {
            var dayLocal = startDayLocal.AddDays(day);

            for (int slot = 0; slot < 2; slot++)
            {
                var startLocal = dayLocal.AddHours(slot * 12);
                var endLocal = startLocal.AddHours(12);

                var shift = new Shift
                {
                    TenantId = tenantId,
                    StartUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, rome),
                    EndUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, rome),
                    Capacity = 2,
                    Location = "Postazione L'Aquila Centro",
                    Notes = $"{SeedMarker} {(slot == 0 ? "Mattina" : "Notte")} {startLocal:dd/MM} {startLocal:HH:mm}–{endLocal:HH:mm}",
                    Status = ShiftStatus.Assigned,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                db.Shifts.Add(shift);

                // Decisione assegnatari:
                //  - giorno 0 (oggi): tutti i turni 0/2  → l'utente può subito provare a prenotarsi
                //  - giorno 1..2: 1/2 → c'è un buco
                //  - giorno 3..7: random tra 1 e 2 → mix
                //  - giorno 8..13: 2/2 → pieni (così la UI mostra anche slot non disponibili)
                int nAssign;
                if (day == 0) nAssign = 0;
                else if (day <= 2) nAssign = 1;
                else if (day <= 7) nAssign = rnd.Next(1, 3);
                else nAssign = 2;

                // Round robin sui medici, evitando di mettere lo stesso medico due volte sullo stesso shift
                var pickedIds = new HashSet<Guid>();
                for (int i = 0; i < nAssign && pickedIds.Count < medici.Count; i++)
                {
                    var medico = medici[(rotation++) % medici.Count];
                    if (!pickedIds.Add(medico.Id))
                    {
                        // se duplicato, scorro avanti
                        i--;
                        continue;
                    }
                    db.ShiftAssignments.Add(new ShiftAssignment
                    {
                        TenantId = tenantId,
                        Shift = shift,
                        MedicoId = medico.Id,
                        IsCurrent = true,
                        AssignedAtUtc = DateTime.UtcNow,
                        CreatedAtUtc = DateTime.UtcNow,
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static TimeZoneInfo TryGetRomeTz()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }
}
