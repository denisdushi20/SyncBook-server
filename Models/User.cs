using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SyncBook.Server.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Password { get; set; }

    [BsonRepresentation(BsonType.String)]
    public UserRole Role { get; set; }

    [BsonRepresentation(BsonType.String)]
    public AuthProvider AuthProvider { get; set; } = AuthProvider.Local;

    public string? GoogleId { get; set; }
}
