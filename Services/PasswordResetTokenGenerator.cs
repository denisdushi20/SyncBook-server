using System.Security.Cryptography;

namespace SyncBook.Server.Services;

public static class PasswordResetTokenGenerator
{
    public static string GenerateUrlSafeToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
