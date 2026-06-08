using System.ComponentModel.DataAnnotations;

namespace SyncBook.Server.Models.Dtos;

public class ChangePasswordRequest
{
    public string? CurrentPassword { get; set; }

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}
