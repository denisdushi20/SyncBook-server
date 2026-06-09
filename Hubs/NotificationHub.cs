using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Models.Dtos;
using SyncBook.Server.Services;

namespace SyncBook.Server.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private const int MaxMessageLength = 2000;

    private readonly MongoDbContext _db;
    private readonly SupportChatDispatcher _chatDispatcher;

    public NotificationHub(MongoDbContext db, SupportChatDispatcher chatDispatcher)
    {
        _db = db;
        _chatDispatcher = chatDispatcher;
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

    public async Task SendMessageToVisitor(SendOwnerChatReplyRequest request)
    {
        if (request is null)
        {
            throw new HubException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.VisitorSessionId))
        {
            throw new HubException("Visitor session id is required.");
        }

        var trimmed = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new HubException("Message is required.");
        }

        if (trimmed.Length > MaxMessageLength)
        {
            throw new HubException($"Message cannot exceed {MaxMessageLength} characters.");
        }

        var businessId = Context.User?.FindFirstValue("businessId");
        if (string.IsNullOrWhiteSpace(businessId))
        {
            throw new HubException("Business id not found in token.");
        }

        var thread = await _db.SupportChatThreads
            .Find(t => t.BusinessId == businessId && t.VisitorSessionId == request.VisitorSessionId)
            .FirstOrDefaultAsync();

        if (thread is null)
        {
            throw new HubException("Chat thread not found.");
        }

        await _chatDispatcher.DispatchOwnerMessageAsync(
            businessId,
            thread.Id,
            request.VisitorSessionId,
            request.VisitorConnectionId,
            trimmed);
    }
}
