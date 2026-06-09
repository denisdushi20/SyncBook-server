using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SyncBook.Server.Models;

[BsonIgnoreExtraElements]
public class SupportChatThread
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string BusinessId { get; set; } = string.Empty;

    public string VisitorSessionId { get; set; } = string.Empty;

    public string? VisitorEmail { get; set; }

    public string? LastVisitorConnectionId { get; set; }

    public DateTime LastMessageAtUtc { get; set; }

    public int UnreadForOwner { get; set; }

    public bool IsActive { get; set; } = true;
}
