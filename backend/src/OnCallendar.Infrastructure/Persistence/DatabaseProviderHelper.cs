using System;

namespace OnCallendar.Infrastructure.Persistence;

/// <summary>
/// Helper statico per leggere il provider DB attivo dall'env, evitando di
/// dover passare ogni volta il contesto. Letto una sola volta.
/// </summary>
public static class DatabaseProviderHelper
{
    // Default = postgres (allineato a Program.cs e a docker-compose.yml).
    // Program.cs chiama Override() in fase di startup con il valore effettivo.
    private static string _provider =
        (Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "postgres")
            .Trim()
            .ToLowerInvariant();

    public static void Override(string provider)
    {
        if (!string.IsNullOrWhiteSpace(provider))
            _provider = provider.Trim().ToLowerInvariant();
    }

    public static string Current => _provider;

    public static bool IsSqlServer => Current is "sqlserver" or "mssql";
    public static bool IsPostgres => Current is "postgres" or "postgresql";
}
