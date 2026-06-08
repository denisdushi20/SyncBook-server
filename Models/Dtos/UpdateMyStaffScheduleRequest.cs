namespace SyncBook.Server.Models.Dtos;

public class UpdateMyStaffScheduleRequest
{
    public bool IsBookable { get; set; }

    public List<StaffWeeklyScheduleEntryDto> WeeklySchedule { get; set; } = [];
}
