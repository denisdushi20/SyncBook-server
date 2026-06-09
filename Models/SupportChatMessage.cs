using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SyncBook.Server.Models;

public enum SupportChatSender
{
    Visitor,
    Owner
}

[BsonIgnoreExtraElements]
public class SupportChatMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ThreadId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string BusinessId { get; set; } = string.Empty;

    public string VisitorSessionId { get; set; } = string.Empty;

    public SupportChatSender From { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; }

    public DateTime? ReadByOwnerAtUtc { get; set; }
}
