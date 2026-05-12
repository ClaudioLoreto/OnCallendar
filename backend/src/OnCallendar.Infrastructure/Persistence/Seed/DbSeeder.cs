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

    /// <summary>
    /// 4 medici del calendario storico di Navelli. Badge = login rapido.
    /// Mappatura corretta (verificata da utente, 8 maggio 2026):
    ///   1 -> Alessandro Marturano
    ///   2 -> Emanuele Du Marteau
    ///   3 -> Edoardo Luci
    ///   4 -> Claudia Ioannucci
    /// I numeri 1..4 sono quelli usati nel JSON dei turni storici 2026.
    /// </summary>
    public static readonly (int Number, string Badge, string Email, string Password, string First, string Last)[] Medici =
    {
        (1, "M01", "medico1@navelli.local", "Medico#2026!", "Alessandro", "Marturano"),
        (2, "M02", "medico2@navelli.local", "Medico#2026!", "Emanuele",   "Du Marteau"),
        (3, "M03", "medico3@navelli.local", "Medico#2026!", "Edoardo",    "Luci"),
        (4, "M04", "medico4@navelli.local", "Medico#2026!", "Claudia",    "Ioannucci"),
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

        // Schema management:
        //  • SqlServer (LocalDB / dev legacy): MigrateAsync con migrations classiche.
        //  • Postgres: MigrateAsync con baseline automatico se il DB era stato
        //    creato in passato con EnsureCreated (prod Railway pre-migrations).
        var providerName = db.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await BaselinePostgresIfLegacyAsync(db, ct);
            await db.Database.MigrateAsync(ct);
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
        // Step 0: migra utenti seed vecchi (formato superboy23+nome@gmail.com) ai
        // nuovi indirizzi medicoN@navelli.local. Solo se il nuovo non esiste ancora.
        var legacyMap = new (string Old, string New)[]
        {
            ("superboy23+alessandro@gmail.com", "medico1@navelli.local"),
            ("superboy23+emanuele@gmail.com",   "medico2@navelli.local"),
            ("superboy23+edoardo@gmail.com",    "medico3@navelli.local"),
            ("superboy23+claudia@gmail.com",    "medico4@navelli.local"),
        };
        foreach (var (oldEmail, newEmail) in legacyMap)
        {
            var legacy = await userManager.FindByEmailAsync(oldEmail);
            if (legacy is null) continue;
            var alreadyNew = await userManager.FindByEmailAsync(newEmail);
            if (alreadyNew is not null) continue;
            legacy.UserName = newEmail;
            legacy.Email = newEmail;
            legacy.NormalizedUserName = userManager.NormalizeName(newEmail);
            legacy.NormalizedEmail = userManager.NormalizeEmail(newEmail);
            legacy.EmailConfirmed = false;
            legacy.IsDefaultEmail = true;
            await userManager.UpdateAsync(legacy);
        }

        // Step 1: crea SOLO se non esiste già un utente con quel Badge.
        // In prod gli utenti possono aver cambiato email, ma conservano il Badge.
        // Senza questo check creeremmo duplicati.
        foreach (var (number, badge, email, password, first, last) in Medici)
        {
            var existsByBadge = await db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Badge == badge, ct);
            if (existsByBadge) continue;

            var user = await userManager.FindByEmailAsync(email);
            if (user is not null) continue;

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                IsDefaultEmail = true,
                FirstName = first,
                LastName = last,
                Role = UserRole.Medico,
                TenantId = tenant.Id,
                MedicoNumber = null,
                Badge = null,
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

        // Svuota il ChangeTracker: le entità caricate da UserManager/FindByEmail
        // possono interferire con le operazioni raw SQL che seguono.
        db.ChangeTracker.Clear();

        // Step 2: aggiorna Badge/MedicoNumber/Nome per ogni medico via SQL.
        // Due strategie in ordine:
        //   a) Se l'utente con quel Badge esiste già (prod): aggiorna solo nome/numero
        //   b) Se non esiste (DB fresco): cerca per email seed e assegna badge
        // Questo evita QUALSIASI conflitto unique IX_AspNetUsers_Badge.
        foreach (var (number, badge, email, _, first, last) in Medici)
        {
            // Prova a) — utente prod con Badge già assegnato
            var affected = await db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE ""AspNetUsers""
                   SET ""MedicoNumber"" = {number},
                       ""FirstName"" = {first},
                       ""LastName"" = {last},
                       ""IsActive"" = true
                   WHERE ""Badge"" = {badge}");

            if (affected > 0) continue;

            // Prova b) — DB fresco, utente appena creato con email seed
            var normalizedEmail = email.ToUpperInvariant();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE ""AspNetUsers""
                   SET ""Badge"" = {badge},
                       ""MedicoNumber"" = {number},
                       ""FirstName"" = {first},
                       ""LastName"" = {last},
                       ""IsActive"" = true
                   WHERE ""NormalizedEmail"" = {normalizedEmail}");
        }

        db.ChangeTracker.Clear();
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

    /// <summary>
    /// Se il DB Postgres contiene già le tabelle applicative (creato in passato
    /// con EnsureCreated, prima dell'introduzione delle migrations) ma manca
    /// __EFMigrationsHistory, popola la storia segnando come "applicate" tutte
    /// le migrations correnti. NON modifica i dati. Idempotente.
    /// </summary>
    private static async Task BaselinePostgresIfLegacyAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct);

        try
        {
            if (await TableExistsAsync(conn, "__EFMigrationsHistory", ct))
                return; // Già gestito da migrations classiche.

            // Tabella sentinella: se non c'è, il DB è vuoto e MigrateAsync creerà tutto.
            if (!await TableExistsAsync(conn, "AspNetUsers", ct))
                return;

            // Legacy DB con dati reali → baseline non distruttivo.
            await using (var create = conn.CreateCommand())
            {
                create.CommandText = @"
CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
    ""MigrationId"" character varying(150) NOT NULL,
    ""ProductVersion"" character varying(32) NOT NULL,
    CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
);";
                await create.ExecuteNonQueryAsync(ct);
            }

            var migrations = db.Database.GetMigrations().ToList();
            const string productVersion = "8.0.10";
            foreach (var migId in migrations)
            {
                await using var ins = conn.CreateCommand();
                ins.CommandText =
                    @"INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                      VALUES (@m, @v) ON CONFLICT DO NOTHING";
                var pm = ins.CreateParameter(); pm.ParameterName = "@m"; pm.Value = migId; ins.Parameters.Add(pm);
                var pv = ins.CreateParameter(); pv.ParameterName = "@v"; pv.Value = productVersion; ins.Parameters.Add(pv);
                await ins.ExecuteNonQueryAsync(ct);
            }
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection conn, string tableName, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT EXISTS (
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = @t)";
        var p = cmd.CreateParameter(); p.ParameterName = "@t"; p.Value = tableName; cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is bool b && b;
    }
}
