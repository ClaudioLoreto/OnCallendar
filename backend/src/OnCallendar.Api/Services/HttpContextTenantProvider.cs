using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OnCallendar.Application.Common.Interfaces;

namespace OnCallendar.Api.Services;

/// <summary>
/// Risolve il tenant corrente leggendo il claim "tenant_id" dal JWT.
/// Per i SuperAdmin il claim è assente -> ritorna null (bypass del filtro).
/// </summary>
public sealed class HttpContextTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _http;
    public HttpContextTenantProvider(IHttpContextAccessor http) => _http = http;

    public Guid? GetCurrentTenantId()
    {
        var claim = _http.HttpContext?.User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public sealed class HttpContextCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    public HttpContextCurrentUserService(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? P => _http.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(P?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => P?.FindFirstValue(ClaimTypes.Name);

    public Guid? TenantId =>
        Guid.TryParse(P?.FindFirstValue("tenant_id"), out var t) ? t : null;

    public bool IsSuperAdmin => P?.IsInRole("SuperAdmin") ?? false;
}
