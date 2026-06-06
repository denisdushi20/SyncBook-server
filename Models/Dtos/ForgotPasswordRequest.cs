using System.ComponentModel.DataAnnotations;

namespace SyncBook.Server.Models.Dtos;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
