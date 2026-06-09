using Microsoft.AspNetCore.SignalR;
using SyncBook.Server.Data;
using SyncBook.Server.Hubs;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;

namespace SyncBook.Server.Services;

public class BookingAlertDispatcher
{
    private readonly MongoDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;

    public BookingAlertDispatcher(MongoDbContext db, IHubContext<NotificationHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    public async Task DispatchAsync(Appointment appointment, string source)
    {
        var notification = new BookingNotification
        {
            AppointmentId = appointment.Id,
            BusinessId = appointment.BusinessId,
            CustomerName = appointment.CustomerName,
            ServiceId = appointment.ServiceId,
            ServiceName = appointment.ServiceName,
            StartUtc = appointment.StartUtc,
            EndUtc = appointment.EndUtc,
            Source = source,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _db.BookingNotifications.InsertOneAsync(notification);

        var payload = new NewBookingAlertDto
        {
            NotificationId = notification.Id,
            AppointmentId = appointment.Id,
            BusinessId = appointment.BusinessId,
            CustomerName = appointment.CustomerName,
            ServiceId = appointment.ServiceId,
            ServiceName = appointment.ServiceName,
            StartUtc = appointment.StartUtc,
            EndUtc = appointment.EndUtc,
            Source = source
        };

        await _hubContext.Clients
            .Group($"booking-alerts:{appointment.BusinessId}")
            .SendAsync("NewBookingAlert", payload);
    }
}
