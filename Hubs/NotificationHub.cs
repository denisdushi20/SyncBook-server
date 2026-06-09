using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using SyncBook.Server.Data;

namespace SyncBook.Server.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly MongoDbContext _db;

    public NotificationHub(MongoDbContext db)
    {
        _db = db;
    }

    public async Task JoinBusinessAlerts(string businessId)
    {
        if (string.IsNullOrWhiteSpace(businessId))
        {
            throw new HubException("Business id is required.");
        }

        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("Not authenticated.");
        }

        var business = await _db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync();
        if (business is null || business.OwnerId != userId)
        {
            throw new HubException("Not authorized for this business.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"booking-alerts:{businessId}");
    }
}
