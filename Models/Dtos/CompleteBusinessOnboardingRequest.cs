using System.ComponentModel.DataAnnotations;

namespace SyncBook.Server.Models.Dtos;

public class CompleteBusinessOnboardingRequest
{
    [Required]
    public BusinessOnboardingRequest Business { get; set; } = new();
}
