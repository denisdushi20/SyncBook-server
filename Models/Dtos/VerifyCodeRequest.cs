using System.ComponentModel.DataAnnotations;

namespace SyncBook.Server.Models.Dtos;

public class VerifyCodeRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}
