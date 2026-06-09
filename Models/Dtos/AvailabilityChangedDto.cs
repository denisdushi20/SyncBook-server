namespace SyncBook.Server.Models.Dtos;

public class AvailabilityChangedDto
{
    public string BusinessId { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }
}
