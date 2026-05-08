using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OnCallendar.Application.Common.Interfaces;

namespace OnCallendar.Infrastructure.Persistence;

/// <summary>
/// Factory usata SOLO da `dotnet ef migrations add/update` a design-time.
/// Forza Npgsql come provider in modo che le migrations generate siano
/// compatibili con Postgres (il DB usato sia in dev che in produzione).
///
/// La connection string indicata qui non viene mai usata per dati reali:
/// EF apre una connessione solo se il comando lo richiede (`database update`),
/// e in quel caso vogliamo che punti al Postgres locale (docker compose up).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("DATABASE_URL_DESIGN")
                   ?? "Host=localhost;Port=5432;Database=oncallendar_dev;Username=oncallendar;Password=dev_only_password";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new ApplicationDbContext(options, new NullTenantProvider());
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public Guid? GetCurrentTenantId() => null;
    }
}
