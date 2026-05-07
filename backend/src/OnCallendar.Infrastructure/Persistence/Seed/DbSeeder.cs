using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;

namespace OnCallendar.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed di Navelli (tenant unico). Crea i 4 medici del calendario storico
/// e importa i 478 turni del 2026 dal file embedded shifts-2026.json.
///
/// Variabili ambiente:
///   ONCALLENDAR_RESET_DB = "true"  → DROP &amp; CREATE database (DEV ONLY).
///   ONCALLENDAR_RESEED_SHIFTS = "true" → ricarica i turni anche se già presenti.
/// </summary>
public static class DbSeeder
{
    public const string SuperAdminRole = "SuperAdmin";
    public const string MedicoRole     = "Medico";

    public const string AdminEmail    = "admin@oncallendar.local";
    public const string AdminPassword = "Admin#2026!";

    public const string NavelliTenantSlug = "navelli";

    /// <summary>4 medici del calendario storico di Navelli. Badge = login rapido.</summary>
    public static readonly (int Number, string Badge, string Email, string Password, string First, string Last)[] Medici =
    {
        (1, "M01", "superboy23+claudia@gmail.com",    "Medico#2026!", "Claudia",    "Ioannucci"),
        (2, "M02", "superboy23+edoardo@gmail.com",    "Medico#2026!", "Edoardo",    "Luci"),
        (3, "M03", "superboy23+emanuele@gmail.com",   "Medico#2026!", "Emanuele",   "Dimarteu"),
        (4, "M04", "superboy23+alessandro@gmail.com", "Medico#2026!", "Alessandro", "Medico4"),
    };

    public static async Task SeedAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken ct = default)
    {
        var resetDb = Environment.GetEnvironmentVariable("ONCALLENDAR_RESET_DB") == "true";
        if (resetDb)
        {
            await db.Database.EnsureDeletedAsync(ct);
        }

        // Le migrations attuali sono SqlServer-specific (nvarchar/datetime2).
        // Su Postgres (Railway) usiamo EnsureCreated, che genera lo schema dal modello
        // attraverso il provider corrente.
        var providerName = db.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.EnsureCreatedAsync(ct);
        }
        else
        {
            await db.Database.MigrateAsync(ct);
        }

        // Roles
        foreach (var role in new[] { SuperAdminRole, MedicoRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        // Tenant Navelli
        var tenant = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == NavelliTenantSlug, ct);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = "Guardia Medica Navelli",
                Slug = NavelliTenantSlug,
                Address = "Navelli (AQ)",
                TimeZoneId = "Europe/Rome",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);
        }

        // SuperAdmin globale
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

        // 4 Medici di Navelli
        foreach (var (number, badge, email, password, first, last) in Medici)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = first,
                    LastName = last,
                    Role = UserRole.Medico,
                    TenantId = tenant.Id,
                    MedicoNumber = number,
                    Badge = badge,
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
            else
            {
                var changed = false;
                if (user.MedicoNumber != number) { user.MedicoNumber = number; changed = true; }
                if (user.Badge != badge)         { user.Badge = badge;         changed = true; }
                if (changed) await userManager.UpdateAsync(user);
            }
        }

        await SeedShiftsAsync(db, tenant.Id, ct);
    }

    private static async Task SeedShiftsAsync(
        ApplicationDbContext db, Guid tenantId, CancellationToken ct)
    {
        var reseed = Environment.GetEnvironmentVariable("ONCALLENDAR_RESEED_SHIFTS") == "true";

        var existing = await db.Shifts.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .CountAsync(ct);

        if (existing > 0 && !reseed) return;

        if (reseed && existing > 0)
        {
            var oldSwaps = await db.SwapRequests.IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId).ToListAsync(ct);
            if (oldSwaps.Count > 0) db.SwapRequests.RemoveRange(oldSwaps);

            var oldShifts = await db.Shifts.IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId).ToListAsync(ct);
            db.Shifts.RemoveRange(oldShifts);
            await db.SaveChangesAsync(ct);
        }

        var medici = await db.Users.IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && u.Role == UserRole.Medico && u.MedicoNumber != null)
            .ToDictionaryAsync(u => u.MedicoNumber!.Value, u => u.Id, ct);
        if (medici.Count < 4) return;

        var rome = TryGetRomeTz();
        var json = LoadShiftsJson();
        var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("shifts");

        var batch = new List<Shift>(arr.GetArrayLength());
        foreach (var entry in arr.EnumerateArray())
        {
            var dateStr     = entry[0].GetString()!;        // "2026-04-30"
            var codeStr     = entry[1].GetString()!;        // "F" / "FN" / ...
            var medTurnoNum = entry[2].GetInt32();
            var medRepNum   = entry[3].GetInt32();

            var date = DateOnly.Parse(dateStr);
            var code = ParseCode(codeStr);
            var (startLocal, endLocal) = ComputeLocalWindow(date, code);

            batch.Add(new Shift
            {
                TenantId = tenantId,
                Date = date,
                Code = code,
                StartUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, rome),
                EndUtc   = TimeZoneInfo.ConvertTimeToUtc(endLocal,   rome),
                MedicoTurnoId      = medici.GetValueOrDefault(medTurnoNum),
                MedicoReperibileId = medici.GetValueOrDefault(medRepNum),
                Status = ShiftStatus.Assigned,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        db.Shifts.AddRange(batch);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Calcola gli orari locali (Europe/Rome) di un turno in base al codice.</summary>
    public static (DateTime startLocal, DateTime endLocal) ComputeLocalWindow(DateOnly date, ShiftCode code)
    {
        var d = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return code switch
        {
            ShiftCode.F  => (d.AddHours(8),  d.AddHours(20)),
            ShiftCode.FN => (d.AddHours(20), d.AddDays(1).AddHours(8)),
            ShiftCode.P  => (d.AddHours(10), d.AddHours(20)),
            ShiftCode.PN => (d.AddHours(20), d.AddDays(1).AddHours(8)),
            ShiftCode.N  => (d.AddHours(20), d.AddDays(1).AddHours(8)),
            _ => throw new ArgumentOutOfRangeException(nameof(code))
        };
    }

    private static ShiftCode ParseCode(string s) => s.ToUpperInvariant() switch
    {
        "F"  => ShiftCode.F,
        "FN" => ShiftCode.FN,
        "P"  => ShiftCode.P,
        "PN" => ShiftCode.PN,
        "N"  => ShiftCode.N,
        _    => throw new ArgumentException($"Codice turno sconosciuto: {s}")
    };

    private static string LoadShiftsJson()
    {
        // Prova prima embedded resource
        var asm = typeof(DbSeeder).Assembly;
        var resource = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("shifts-2026.json", StringComparison.OrdinalIgnoreCase));
        if (resource is not null)
        {
            using var s = asm.GetManifestResourceStream(resource)!;
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }

        // Fallback su file su disco (utile in dev quando il file non è embedded)
        var asmDir = Path.GetDirectoryName(asm.Location)!;
        var candidates = new[]
        {
            Path.Combine(asmDir, "Persistence", "Seed", "shifts-2026.json"),
            Path.Combine(AppContext.BaseDirectory, "shifts-2026.json"),
            Path.Combine(AppContext.BaseDirectory, "Persistence", "Seed", "shifts-2026.json"),
            // Risale fino a backend/src/OnCallendar.Infrastructure/Persistence/Seed/shifts-2026.json
            Path.Combine(asmDir, "..", "..", "..", "..", "OnCallendar.Infrastructure",
                          "Persistence", "Seed", "shifts-2026.json"),
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(
            "shifts-2026.json non trovato (né come embedded resource né su disco).");
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
