using SyncBook.Server.Models;

namespace SyncBook.Server.Models.Dtos;

public class AppointmentDto
{
    public string Id { get; set; } = string.Empty;

    public string BusinessId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string ServiceId { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public int BufferMinutes { get; set; }

    public string? StaffId { get; set; }

    public string? StaffName { get; set; }

    public AppointmentStatus Status { get; set; }
}
