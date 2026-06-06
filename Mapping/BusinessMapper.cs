using MongoDB.Bson;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;

namespace SyncBook.Server.Mapping;

public static class BusinessMapper
{
    public static List<BusinessService> NormalizeServices(IEnumerable<BusinessService>? services)
    {
        return (services ?? [])
            .Select(s => new BusinessService
            {
                Id = string.IsNullOrWhiteSpace(s.Id) ? ObjectId.GenerateNewId().ToString() : s.Id,
                Name = s.Name,
                DurationMinutes = s.DurationMinutes
            })
            .ToList();
    }

    public static BusinessPublicDto ToPublicDto(Business business) => new()
    {
        Id = business.Id,
        Name = business.Name,
        Email = business.Email,
        Phone = business.Phone,
        Description = business.Description,
        Category = business.Category,
        Image = business.Image,
        Services = NormalizeServices(business.Services),
        WorkingHours = business.WorkingHours ?? []
    };

    public static AppointmentDto ToDto(Appointment appointment) => new()
    {
        Id = appointment.Id,
        BusinessId = appointment.BusinessId,
        CustomerName = appointment.CustomerName,
        CustomerEmail = appointment.CustomerEmail,
        ServiceName = appointment.ServiceName,
        StartUtc = appointment.StartUtc,
        EndUtc = appointment.EndUtc,
        Status = appointment.Status
    };
}
