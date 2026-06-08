using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SyncBook.Server.Models;

public enum AppointmentStatus
{
    Pending,
    Confirmed,
    Cancelled
}

[BsonIgnoreExtraElements]
public class Appointment
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string BusinessId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ServiceId { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public int BufferMinutes { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? StaffId { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
}
