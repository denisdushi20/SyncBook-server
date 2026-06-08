using SyncBook.Server.Models;

namespace SyncBook.Server.Models.Dtos;

public class TodayAppointmentSummaryDto
{
    public string Id { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; }
}
