using System.ComponentModel.DataAnnotations;
using SyncBook.Server.Models;

namespace SyncBook.Server.Models.Dtos;

public class RegisterRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; }

    public BusinessOnboardingRequest? Business { get; set; }
}
