using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OnCallendar.Domain.Entities;

namespace OnCallendar.Api.Auth;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "OnCallendar";
    public string Audience { get; set; } = "OnCallendar.Mobile";
    public string SecretKey { get; set; } = "dev-only-please-change-in-prod-32+chars-secret-key!!";
    public int ExpiryMinutes { get; set; } = 60 * 8;
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) Create(ApplicationUser user, IList<string> roles);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _s;

    public JwtTokenService(JwtSettings settings) => _s = settings;

    public (string Token, DateTime ExpiresAtUtc) Create(ApplicationUser user, IList<string> roles)
    {
        var expires = DateTime.UtcNow.AddMinutes(_s.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new("first_name", user.FirstName),
            new("last_name",  user.LastName),
        };

        if (user.TenantId.HasValue)
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));

        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_s.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _s.Issuer,
            audience: _s.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
