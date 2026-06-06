using MongoDB.Bson;

namespace SyncBook.Server.Models;

public class BusinessService
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Name { get; set; } = string.Empty;

    public int? DurationMinutes { get; set; }
}
