using System;

namespace OnCallendar.Infrastructure.Persistence;

/// <summary>
/// Helper statico per leggere il provider DB attivo dall'env, evitando di
/// dover passare ogni volta il contesto. Letto una sola volta.
/// </summary>
public static class DatabaseProviderHelper
{
    private static readonly Lazy<string> _provider = new(() =>
        (Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "sqlserver")
            .Trim()
            .ToLowerInvariant());

    public static string Current => _provider.Value;

    public static bool IsSqlServer => Current is "sqlserver" or "mssql";
    public static bool IsPostgres => Current is "postgres" or "postgresql";
}
