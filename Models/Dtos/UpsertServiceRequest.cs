namespace SyncBook.Server.Models.Dtos;

public class UpsertServiceRequest
{
    public string Name { get; set; } = string.Empty;

    public int? DurationMinutes { get; set; }
}
