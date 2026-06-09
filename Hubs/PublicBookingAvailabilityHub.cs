using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SyncBook.Server.Hubs;

[AllowAnonymous]
public class PublicBookingAvailabilityHub : Hub
{
    public Task JoinAvailability(string businessId)
    {
        if (string.IsNullOrWhiteSpace(businessId))
        {
            throw new HubException("Business id is required.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, $"availability:{businessId}");
    }
}
