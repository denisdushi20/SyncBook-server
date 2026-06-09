using System.ComponentModel.DataAnnotations;

namespace SyncBook.Server.Services;

public static class VisitorEmailHelper
{
    public const int MaxLength = 254;

    public static bool TryNormalize(string? raw, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Visitor email is required.";
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length > MaxLength)
        {
            error = $"Email cannot exceed {MaxLength} characters.";
            return false;
        }

        if (!new EmailAddressAttribute().IsValid(trimmed))
        {
            error = "Invalid email address.";
            return false;
        }

        normalized = trimmed.ToLowerInvariant();
        return true;
    }
}
