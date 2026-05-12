using System.Security.Cryptography;

namespace OnCallendar.Application.Common;

public static class TokenGenerator
{
    public static string GenerateUrlSafeToken(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
