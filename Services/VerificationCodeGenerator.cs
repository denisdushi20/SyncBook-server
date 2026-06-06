using System.Security.Cryptography;

namespace SyncBook.Server.Services;

public static class VerificationCodeGenerator
{
    public static string GenerateSixDigitCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }
}
