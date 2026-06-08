using SyncBook.Server.Models;

namespace SyncBook.Server.Services;

public static class StaffBootstrapHelper
{
    private static readonly DayOfWeek[] AllDays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    public static List<StaffWeeklyScheduleEntry> DeriveWeeklyScheduleFromBusinessHours(
        IReadOnlyList<DaySchedule>? workingHours)
    {
        if (workingHours is null || workingHours.Count == 0)
        {
            return CreateDefaultWeeklySchedule();
        }

        var schedule = new List<StaffWeeklyScheduleEntry>();
        foreach (var day in AllDays)
        {
            var dayName = day.ToString();
            var businessDay = workingHours.FirstOrDefault(d =>
                string.Equals(d.Day, dayName, StringComparison.OrdinalIgnoreCase));

            if (businessDay is null)
            {
                schedule.Add(new StaffWeeklyScheduleEntry
                {
                    DayOfWeek = day,
                    IsAvailable = true,
                    StartTime = "08:00",
                    EndTime = "17:00"
                });
                continue;
            }

            schedule.Add(new StaffWeeklyScheduleEntry
            {
                DayOfWeek = day,
                IsAvailable = businessDay.IsOpen,
                StartTime = businessDay.IsOpen ? businessDay.OpenTime : null,
                EndTime = businessDay.IsOpen ? businessDay.CloseTime : null
            });
        }

        return schedule;
    }

    public static StaffMember CreateOwnerStaffMember(
        string userId,
        string businessId,
        IReadOnlyList<DaySchedule>? workingHours) => new()
    {
        UserId = userId,
        BusinessId = businessId,
        IsBookable = true,
        WeeklySchedule = DeriveWeeklyScheduleFromBusinessHours(workingHours)
    };

    private static List<StaffWeeklyScheduleEntry> CreateDefaultWeeklySchedule() =>
        AllDays.Select(day => new StaffWeeklyScheduleEntry
        {
            DayOfWeek = day,
            IsAvailable = true,
            StartTime = "08:00",
            EndTime = "17:00"
        }).ToList();
}
