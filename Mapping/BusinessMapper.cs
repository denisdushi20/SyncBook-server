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
                DurationMinutes = s.DurationMinutes,
                Price = s.Price
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

    public static AppointmentDto ToDto(Appointment appointment, string? staffName = null) => new()
    {
        Id = appointment.Id,
        BusinessId = appointment.BusinessId,
        CustomerName = appointment.CustomerName,
        CustomerEmail = appointment.CustomerEmail,
        CustomerPhone = appointment.CustomerPhone,
        ServiceId = appointment.ServiceId,
        ServiceName = appointment.ServiceName,
        ServicePrice = appointment.ServicePrice,
        StartUtc = appointment.StartUtc,
        EndUtc = appointment.EndUtc,
        BufferMinutes = appointment.BufferMinutes,
        StaffId = appointment.StaffId,
        StaffName = staffName,
        Status = appointment.Status
    };

    public static StaffMemberDto ToStaffDto(StaffMember staff, string fullName) => new()
    {
        Id = staff.Id,
        UserId = staff.UserId,
        BusinessId = staff.BusinessId,
        FullName = fullName,
        IsBookable = staff.IsBookable,
        WeeklySchedule = (staff.WeeklySchedule ?? []).Select(e => new StaffWeeklyScheduleEntryDto
        {
            DayOfWeek = e.DayOfWeek,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            IsAvailable = e.IsAvailable
        }).ToList()
    };
}
