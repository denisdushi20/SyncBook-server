using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;
using SyncBook.Server.Services;

namespace SyncBook.Server.Controllers;

[ApiController]
[Route("api")]
public class SupportChatController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly CurrentUserService _currentUser;
    private readonly SupportChatDispatcher _chatDispatcher;

    public SupportChatController(
        MongoDbContext db,
        CurrentUserService currentUser,
        SupportChatDispatcher chatDispatcher)
    {
        _db = db;
        _currentUser = currentUser;
        _chatDispatcher = chatDispatcher;
    }

    [HttpPost("support-chat/visitor/messages")]
    [AllowAnonymous]
    public async Task<ActionResult<VisitorChatMessageDto>> SendVisitorMessage(
        [FromBody] SendVisitorMessageRequest request)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Request is required." });
        }

        if (string.IsNullOrWhiteSpace(request.BusinessId))
        {
            return BadRequest(new { message = "Business id is required." });
        }

        if (string.IsNullOrWhiteSpace(request.VisitorSessionId))
        {
            return BadRequest(new { message = "Visitor session id is required." });
        }

        if (!VisitorEmailHelper.TryNormalize(request.VisitorEmail, out var normalizedEmail, out var emailError))
        {
            return BadRequest(new { message = emailError });
        }

        var trimmed = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return BadRequest(new { message = "Message is required." });
        }

        var business = await _db.Businesses.Find(b => b.Id == request.BusinessId).FirstOrDefaultAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        var payload = await _chatDispatcher.DispatchVisitorMessageAsync(
            request.BusinessId,
            normalizedEmail,
            request.VisitorSessionId,
            request.VisitorConnectionId ?? string.Empty,
            trimmed);

        return Ok(payload);
    }

    [HttpGet("support-chat/visitor/{businessId}/history")]
    [AllowAnonymous]
    public async Task<ActionResult<List<SupportChatMessageDto>>> GetVisitorHistory(
        string businessId,
        [FromQuery] string? email,
        [FromQuery] string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { message = "Email or session id is required." });
        }

        var business = await _db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        SupportChatThread? thread = null;

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (!VisitorEmailHelper.TryNormalize(email, out var normalizedEmail, out var emailError))
            {
                return BadRequest(new { message = emailError });
            }

            thread = await _db.SupportChatThreads
                .Find(t => t.BusinessId == businessId && t.VisitorEmail == normalizedEmail)
                .FirstOrDefaultAsync();
        }

        if (thread is null && !string.IsNullOrWhiteSpace(sessionId))
        {
            thread = await _db.SupportChatThreads
                .Find(t => t.BusinessId == businessId && t.VisitorSessionId == sessionId)
                .FirstOrDefaultAsync();
        }

        if (thread is null)
        {
            return Ok(new List<SupportChatMessageDto>());
        }

        var messages = await _db.SupportChatMessages
            .Find(m => m.ThreadId == thread.Id)
            .SortBy(m => m.SentAtUtc)
            .ToListAsync();

        return Ok(messages.Select(ToMessageDto).ToList());
    }

    [HttpGet("businesses/me/support-chats")]
    [Authorize(Policy = "BusinessOwner")]
    public async Task<ActionResult<List<SupportChatThreadDto>>> GetOwnerThreads()
    {
        var businessId = _currentUser.BusinessId;
        if (string.IsNullOrWhiteSpace(businessId))
        {
            return NotFound(new { message = "Business not found." });
        }

        var threads = await _db.SupportChatThreads
            .Find(t => t.BusinessId == businessId && t.IsActive)
            .SortByDescending(t => t.LastMessageAtUtc)
            .ToListAsync();

        var result = new List<SupportChatThreadDto>();
        foreach (var thread in threads)
        {
            var lastMessage = await _db.SupportChatMessages
                .Find(m => m.ThreadId == thread.Id)
                .SortByDescending(m => m.SentAtUtc)
                .Limit(1)
                .FirstOrDefaultAsync();

            result.Add(new SupportChatThreadDto
            {
                Id = thread.Id,
                BusinessId = thread.BusinessId,
                VisitorSessionId = thread.VisitorSessionId,
                VisitorEmail = thread.VisitorEmail,
                LastVisitorConnectionId = thread.LastVisitorConnectionId,
                LastMessageAtUtc = thread.LastMessageAtUtc,
                UnreadForOwner = thread.UnreadForOwner,
                LastMessagePreview = lastMessage?.Text
            });
        }

        return Ok(result);
    }

    [HttpGet("businesses/me/support-chats/{threadId}/messages")]
    [Authorize(Policy = "BusinessOwner")]
    public async Task<ActionResult<List<SupportChatMessageDto>>> GetOwnerThreadMessages(string threadId)
    {
        var businessId = _currentUser.BusinessId;
        if (string.IsNullOrWhiteSpace(businessId))
        {
            return NotFound(new { message = "Business not found." });
        }

        var thread = await _db.SupportChatThreads
            .Find(t => t.Id == threadId && t.BusinessId == businessId)
            .FirstOrDefaultAsync();

        if (thread is null)
        {
            return NotFound(new { message = "Thread not found." });
        }

        var messages = await _db.SupportChatMessages
            .Find(m => m.ThreadId == threadId)
            .SortBy(m => m.SentAtUtc)
            .ToListAsync();

        return Ok(messages.Select(ToMessageDto).ToList());
    }

    [HttpPost("businesses/me/support-chats/{threadId}/read")]
    [Authorize(Policy = "BusinessOwner")]
    public async Task<IActionResult> MarkThreadRead(string threadId)
    {
        var businessId = _currentUser.BusinessId;
        if (string.IsNullOrWhiteSpace(businessId))
        {
            return NotFound(new { message = "Business not found." });
        }

        var result = await _db.SupportChatThreads.UpdateOneAsync(
            t => t.Id == threadId && t.BusinessId == businessId,
            Builders<SupportChatThread>.Update.Set(t => t.UnreadForOwner, 0));

        if (result.MatchedCount == 0)
        {
            return NotFound(new { message = "Thread not found." });
        }

        var now = DateTime.UtcNow;
        await _db.SupportChatMessages.UpdateManyAsync(
            m => m.ThreadId == threadId
                && m.From == SupportChatSender.Visitor
                && m.ReadByOwnerAtUtc == null,
            Builders<SupportChatMessage>.Update.Set(m => m.ReadByOwnerAtUtc, now));

        return NoContent();
    }

    private static SupportChatMessageDto ToMessageDto(SupportChatMessage message) => new()
    {
        Id = message.Id,
        ThreadId = message.ThreadId,
        BusinessId = message.BusinessId,
        VisitorSessionId = message.VisitorSessionId,
        From = message.From,
        Text = message.Text,
        SentAtUtc = message.SentAtUtc
    };
}
