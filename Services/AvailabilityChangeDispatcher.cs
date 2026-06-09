using Microsoft.AspNetCore.SignalR;
using SyncBook.Server.Hubs;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;

namespace SyncBook.Server.Services;

public class AvailabilityChangeDispatcher
{
    private readonly IHubContext<PublicBookingAvailabilityHub> _hubContext;

    public AvailabilityChangeDispatcher(IHubContext<PublicBookingAvailabilityHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task DispatchAsync(string businessId, DateTime startUtc, DateTime endUtc)
    {
        var payload = new AvailabilityChangedDto
        {
            BusinessId = businessId,
            StartUtc = startUtc,
            EndUtc = endUtc
        };

        await _hubContext.Clients
            .Group($"availability:{businessId}")
            .SendAsync("AvailabilityChanged", payload);
    }

    public Task DispatchConfigChangedAsync(string businessId, List<DaySchedule> workingHours)
    {
        return _hubContext.Clients
            .Group($"availability:{businessId}")
            .SendAsync("BusinessConfigChanged", businessId, workingHours);
    }

    public Task DispatchStatusChangedAsync(string businessId, bool isLive)
    {
        return _hubContext.Clients
            .Group($"availability:{businessId}")
            .SendAsync("BusinessStatusChanged", businessId, isLive);
    }
}
