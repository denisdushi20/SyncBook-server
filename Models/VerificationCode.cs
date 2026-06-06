using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SyncBook.Server.Models;

[BsonIgnoreExtraElements]
public class VerificationCode
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public VerificationCodePurpose Purpose { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }
}
