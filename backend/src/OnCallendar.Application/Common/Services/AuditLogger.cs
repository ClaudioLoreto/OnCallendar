using System.Text.Json;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Entities;

namespace OnCallendar.Application.Common.Services;

public interface IAuditLogger
{
    void Log(
        string entityType,
        Guid entityId,
        string action,
        Guid tenantId,
        object? oldValues = null,
        object? newValues = null,
        string? notes = null);
}

public sealed class AuditLogger : IAuditLogger
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public AuditLogger(IApplicationDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public void Log(
        string entityType, Guid entityId, string action, Guid tenantId,
        object? oldValues = null, object? newValues = null, string? notes = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            PerformedByUserId = _user.UserId,
            PerformedByUserName = _user.UserName,
            PerformedAtUtc = DateTime.UtcNow,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOpts),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOpts),
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }
}
