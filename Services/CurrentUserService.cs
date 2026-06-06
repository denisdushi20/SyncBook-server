using System.Security.Claims;
using SyncBook.Server.Models;

namespace SyncBook.Server.Services;

public class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

    public UserRole? Role
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null) return null;

            var roleClaim = user.FindFirstValue("role")
                ?? user.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(roleClaim, out var role) ? role : null;
        }
    }

    public string? BusinessId =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("businessId");

    public bool IsBusinessOwner => Role == UserRole.BusinessOwner && !string.IsNullOrEmpty(BusinessId);
}
