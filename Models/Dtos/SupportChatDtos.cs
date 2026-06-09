using System.ComponentModel.DataAnnotations;
using SyncBook.Server.Models;

namespace SyncBook.Server.Models.Dtos;

public record VisitorChatMessageDto(
    string MessageId,
    string ThreadId,
    string BusinessId,
    string Message,
    string VisitorConnectionId,
    string VisitorSessionId,
    string VisitorEmail,
    DateTime SentAtUtc);

public class SupportChatThreadDto
{
    public string Id { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public string VisitorSessionId { get; set; } = string.Empty;
    public string? VisitorEmail { get; set; }
    public string? LastVisitorConnectionId { get; set; }
    public DateTime LastMessageAtUtc { get; set; }
    public int UnreadForOwner { get; set; }
    public string? LastMessagePreview { get; set; }
}

public class SupportChatMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public string VisitorSessionId { get; set; } = string.Empty;
    public SupportChatSender From { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
}

public class SendOwnerChatReplyRequest
{
    public string VisitorSessionId { get; set; } = string.Empty;
    public string? VisitorConnectionId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SendVisitorMessageRequest
{
    public string BusinessId { get; set; } = string.Empty;
    public string VisitorSessionId { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(254)]
    public string VisitorEmail { get; set; } = string.Empty;

    public string? VisitorConnectionId { get; set; }
    public string Message { get; set; } = string.Empty;
}
