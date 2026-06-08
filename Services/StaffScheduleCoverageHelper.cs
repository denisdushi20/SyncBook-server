using SyncBook.Server.Models;

namespace SyncBook.Server.Services;

public static class StaffScheduleCoverageHelper
{
    public static StaffWeeklyScheduleEntry? ResolveDayEntry(StaffMember staff, DayOfWeek day) =>
        staff.WeeklySchedule?.FirstOrDefault(e => e.DayOfWeek == day);

    public static bool CoversInterval(StaffMember staff, DateTime startUtc, DateTime endUtc)
    {
        if (startUtc.Date != endUtc.Date)
        {
            return false;
        }

        var entry = ResolveDayEntry(staff, startUtc.DayOfWeek);
        if (entry is null || !entry.IsAvailable
            || string.IsNullOrWhiteSpace(entry.StartTime)
            || string.IsNullOrWhiteSpace(entry.EndTime))
        {
            return false;
        }

        if (!TimeOnly.TryParse(entry.StartTime, out var shiftStart)
            || !TimeOnly.TryParse(entry.EndTime, out var shiftEnd))
        {
            return false;
        }

        var slotStart = TimeOnly.FromDateTime(startUtc);
        var slotEnd = TimeOnly.FromDateTime(endUtc);

        return slotStart >= shiftStart && slotEnd <= shiftEnd;
    }
}
