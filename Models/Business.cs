using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SyncBook.Server.Models;

[BsonIgnoreExtraElements]
public class Business
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? Image { get; set; }

    public List<BusinessService> Services { get; set; } = [];

    public List<DaySchedule> WorkingHours { get; set; } = [];

    public bool? IsLive { get; set; }

    public string? SelectedPlan { get; set; }

    public string? StripeCustomerId { get; set; }

    public string? StripeSubscriptionId { get; set; }

    public string? SubscriptionStatus { get; set; }

    public DateTime? SubscriptionPaidAt { get; set; }
}
