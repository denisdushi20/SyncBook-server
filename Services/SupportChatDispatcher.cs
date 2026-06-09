using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Hubs;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;

namespace SyncBook.Server.Services;

public class SupportChatDispatcher
{
    private readonly MongoDbContext _db;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly IHubContext<PublicBookingAvailabilityHub> _publicHub;

    public SupportChatDispatcher(
        MongoDbContext db,
        IHubContext<NotificationHub> notificationHub,
        IHubContext<PublicBookingAvailabilityHub> publicHub)
    {
        _db = db;
        _notificationHub = notificationHub;
        _publicHub = publicHub;
    }

    public async Task<VisitorChatMessageDto> DispatchVisitorMessageAsync(
        string businessId,
        string visitorEmail,
        string visitorSessionId,
        string visitorConnectionId,
        string message)
    {
        if (!VisitorEmailHelper.TryNormalize(visitorEmail, out var normalizedEmail, out var emailError))
        {
            throw new HubException(emailError ?? "Invalid visitor email.");
        }

        var sentAtUtc = DateTime.UtcNow;
        var thread = await UpsertThreadForVisitorAsync(
            businessId,
            normalizedEmail,
            visitorSessionId,
            visitorConnectionId,
            sentAtUtc,
            incrementUnread: true);

        var chatMessage = new SupportChatMessage
        {
            ThreadId = thread.Id,
            BusinessId = businessId,
            VisitorSessionId = visitorSessionId,
            From = SupportChatSender.Visitor,
            Text = message,
            SentAtUtc = sentAtUtc
        };

        await _db.SupportChatMessages.InsertOneAsync(chatMessage);

        var payload = new VisitorChatMessageDto(
            chatMessage.Id,
            thread.Id,
            businessId,
            message,
            visitorConnectionId,
            visitorSessionId,
            normalizedEmail,
            sentAtUtc);

        await _notificationHub.Clients
            .Group($"booking-alerts:{businessId}")
            .SendAsync("ReceiveVisitorMessage", payload);

        return payload;
    }

    public async Task<SupportChatMessageDto> DispatchOwnerMessageAsync(
        string businessId,
        string threadId,
        string visitorSessionId,
        string? visitorConnectionId,
        string message)
    {
        var sentAtUtc = DateTime.UtcNow;

        var chatMessage = new SupportChatMessage
        {
            ThreadId = threadId,
            BusinessId = businessId,
            VisitorSessionId = visitorSessionId,
            From = SupportChatSender.Owner,
            Text = message,
            SentAtUtc = sentAtUtc
        };

        await _db.SupportChatMessages.InsertOneAsync(chatMessage);

        await _db.SupportChatThreads.UpdateOneAsync(
            t => t.Id == threadId,
            Builders<SupportChatThread>.Update
                .Set(t => t.LastMessageAtUtc, sentAtUtc)
                .Set(t => t.LastVisitorConnectionId, visitorConnectionId));

        if (!string.IsNullOrWhiteSpace(visitorConnectionId))
        {
            await _publicHub.Clients
                .Client(visitorConnectionId)
                .SendAsync("ReceiveOwnerMessage", message);
        }

        return new SupportChatMessageDto
        {
            Id = chatMessage.Id,
            ThreadId = threadId,
            BusinessId = businessId,
            VisitorSessionId = visitorSessionId,
            From = SupportChatSender.Owner,
            Text = message,
            SentAtUtc = sentAtUtc
        };
    }

    private async Task<SupportChatThread> UpsertThreadForVisitorAsync(
        string businessId,
        string normalizedEmail,
        string visitorSessionId,
        string visitorConnectionId,
        DateTime sentAtUtc,
        bool incrementUnread)
    {
        var existing = await _db.SupportChatThreads
            .Find(t => t.BusinessId == businessId && t.VisitorEmail == normalizedEmail)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            var update = Builders<SupportChatThread>.Update
                .Set(t => t.LastMessageAtUtc, sentAtUtc)
                .Set(t => t.LastVisitorConnectionId, visitorConnectionId)
                .Set(t => t.VisitorSessionId, visitorSessionId)
                .Set(t => t.IsActive, true);

            if (incrementUnread)
            {
                update = update.Inc(t => t.UnreadForOwner, 1);
            }

            await _db.SupportChatThreads.UpdateOneAsync(t => t.Id == existing.Id, update);
            existing.LastMessageAtUtc = sentAtUtc;
            existing.LastVisitorConnectionId = visitorConnectionId;
            existing.VisitorSessionId = visitorSessionId;
            if (incrementUnread)
            {
                existing.UnreadForOwner += 1;
            }

            return existing;
        }

        var thread = new SupportChatThread
        {
            BusinessId = businessId,
            VisitorEmail = normalizedEmail,
            VisitorSessionId = visitorSessionId,
            LastVisitorConnectionId = visitorConnectionId,
            LastMessageAtUtc = sentAtUtc,
            UnreadForOwner = incrementUnread ? 1 : 0,
            IsActive = true
        };

        await _db.SupportChatThreads.InsertOneAsync(thread);
        return thread;
    }
}
