using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SyncBook.Server.Models;

[BsonIgnoreExtraElements]
public class StaffMember
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string BusinessId { get; set; } = string.Empty;

    public bool IsBookable { get; set; } = true;

    public List<StaffWeeklyScheduleEntry> WeeklySchedule { get; set; } = [];
}

public class StaffWeeklyScheduleEntry
{
    public DayOfWeek DayOfWeek { get; set; }

    public string? StartTime { get; set; }

    public string? EndTime { get; set; }

    public bool IsAvailable { get; set; }
}
