namespace OnCallendar.Application.Common.Interfaces;

public interface ITenantProvider
{
    /// <summary>TenantId dell'utente corrente. Null se SuperAdmin globale.</summary>
    Guid? GetCurrentTenantId();
}

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    Guid? TenantId { get; }
    bool IsSuperAdmin { get; }
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
