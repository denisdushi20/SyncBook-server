using SyncBook.Server.Models.Dtos;

namespace SyncBook.Server.Validation;

public static class StaffWeeklyScheduleValidator
{
    public static string? Validate(IReadOnlyList<StaffWeeklyScheduleEntryDto>? weeklySchedule)
    {
        if (weeklySchedule is null || weeklySchedule.Count != 7)
        {
            return "Weekly schedule must contain exactly 7 days.";
        }

        var seenDays = new HashSet<DayOfWeek>();
        foreach (var entry in weeklySchedule)
        {
            if (!seenDays.Add(entry.DayOfWeek))
            {
                return $"Duplicate schedule entry for {entry.DayOfWeek}.";
            }

            if (entry.IsAvailable)
            {
                if (string.IsNullOrWhiteSpace(entry.StartTime) || string.IsNullOrWhiteSpace(entry.EndTime))
                {
                    return $"Start and end times are required for {entry.DayOfWeek} when available.";
                }

                if (!TimeOnly.TryParse(entry.StartTime, out var startTime)
                    || !TimeOnly.TryParse(entry.EndTime, out var endTime))
                {
                    return $"Invalid time format for {entry.DayOfWeek}. Use HH:mm.";
                }

                if (startTime >= endTime)
                {
                    return $"Start time must be before end time for {entry.DayOfWeek}.";
                }
            }
        }

        if (seenDays.Count != 7)
        {
            return "Weekly schedule must include all 7 days of the week.";
        }

        return null;
    }
}
