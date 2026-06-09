using SyncBook.Server.Models;

namespace SyncBook.Server.Models.Dtos;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;

    public UserSnapshot User { get; set; } = new();
}

public class UserSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public string? BusinessId { get; set; }

    public bool SubscriptionActive { get; set; } = true;
}

public class RegisterResponse
{
    public UserSnapshot User { get; set; } = new();
}
