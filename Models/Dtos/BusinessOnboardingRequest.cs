using System.ComponentModel.DataAnnotations;
using SyncBook.Server.Models;

namespace SyncBook.Server.Models.Dtos;

public class BusinessOnboardingRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? Image { get; set; }

    [Required]
    [MinLength(1)]
    public List<BusinessService> Services { get; set; } = [];

    [Required]
    [MinLength(7)]
    public List<DaySchedule> WorkingHours { get; set; } = [];
}
