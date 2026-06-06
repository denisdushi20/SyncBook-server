using SyncBook.Server.Models;

namespace SyncBook.Server.Models.Dtos;

public class UpdateWorkingHoursRequest
{
    public List<DaySchedule> WorkingHours { get; set; } = [];
}
