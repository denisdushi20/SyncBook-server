namespace SyncBook.Server.Models.Dtos;

public class InternalAppointmentSlotDto
{
    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string Label { get; set; } = string.Empty;
}
