using SyncBook.Server.Models;

namespace SyncBook.Server.Models.Dtos;

public class BusinessPublicDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? Image { get; set; }

    public List<BusinessService> Services { get; set; } = [];

    public List<DaySchedule> WorkingHours { get; set; } = [];

    public bool IsLive { get; set; }
}
