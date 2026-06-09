namespace SyncBook.Server.Models.Dtos;

public class CreateStaffRequest
{
    public string FullName { get; set; } = string.Empty;

    public bool IsBookable { get; set; } = true;

    public List<StaffWeeklyScheduleEntryDto>? WeeklySchedule { get; set; }
}
