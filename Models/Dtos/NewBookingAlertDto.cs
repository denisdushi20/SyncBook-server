namespace SyncBook.Server.Models.Dtos;

public class NewBookingAlertDto
{
    public string NotificationId { get; set; } = string.Empty;

    public string AppointmentId { get; set; } = string.Empty;

    public string BusinessId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string ServiceId { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string Source { get; set; } = string.Empty;
}
