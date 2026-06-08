namespace SyncBook.Server.Models.Dtos;

public class StaffMemberDto
{
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string BusinessId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsBookable { get; set; }

    public List<StaffWeeklyScheduleEntryDto> WeeklySchedule { get; set; } = [];
}

public class StaffWeeklyScheduleEntryDto
{
    public DayOfWeek DayOfWeek { get; set; }

    public string? StartTime { get; set; }

    public string? EndTime { get; set; }

    public bool IsAvailable { get; set; }
}
