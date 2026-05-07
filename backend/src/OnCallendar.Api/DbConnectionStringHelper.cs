using System;
using Npgsql;

namespace OnCallendar.Api;

/// <summary>
/// Converte gli URL stile "postgres://user:pass@host:port/db" forniti da
/// Railway/Heroku in una connection string compatibile con Npgsql.
/// Se la stringa è già in formato Npgsql (Host=...;Username=...) la lascia.
/// </summary>
public static class DbConnectionStringHelper
{
    public static string NormalizeNpgsql(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Connection string vuota", nameof(raw));

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(trimmed);
            var userInfo = uri.UserInfo.Split(':', 2);
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
            var database = uri.AbsolutePath.TrimStart('/');
            var port = uri.Port > 0 ? uri.Port : 5432;

            var sb = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = port,
                Username = username,
                Password = password,
                Database = database,
                SslMode = SslMode.Require,
                TrustServerCertificate = true,
                Pooling = true,
            };
            return sb.ConnectionString;
        }

        return trimmed;
    }
}
