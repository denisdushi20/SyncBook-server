using SyncBook.Server.Models;

namespace SyncBook.Server.Models.Dtos;

public class UserProfileResponse
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public AuthProvider AuthProvider { get; set; }

    public string? BusinessId { get; set; }
}
