namespace SyncBook.Server.Models.Dtos;

public class CreatePublicAppointmentRequest
{
    public string BusinessId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string ServiceId { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }
}
