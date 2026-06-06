using SyncBook.Server.Models;

namespace SyncBook.Server.Validation;

public static class WorkingHoursValidator
{
    public static string? Validate(IReadOnlyList<DaySchedule>? workingHours)
    {
        if (workingHours is null || workingHours.Count < 7)
        {
            return "Working hours for all 7 days are required.";
        }

        foreach (var day in workingHours)
        {
            if (day.IsOpen && (string.IsNullOrWhiteSpace(day.OpenTime) || string.IsNullOrWhiteSpace(day.CloseTime)))
            {
                return $"Open and close times are required for {day.Day} when the business is open.";
            }
        }

        return null;
    }
}
