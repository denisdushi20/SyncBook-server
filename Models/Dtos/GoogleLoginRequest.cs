using System.ComponentModel.DataAnnotations;

namespace SyncBook.Server.Models.Dtos;

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}
