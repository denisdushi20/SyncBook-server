namespace SyncBook.Server.Models;

public class DaySchedule
{
    public string Day { get; set; } = string.Empty;

    public bool IsOpen { get; set; }

    public string? OpenTime { get; set; }

    public string? CloseTime { get; set; }
}
