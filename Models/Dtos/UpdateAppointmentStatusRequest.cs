using SyncBook.Server.Models;

namespace SyncBook.Server.Models.Dtos;

public class UpdateAppointmentStatusRequest
{
    public AppointmentStatus Status { get; set; }
}
