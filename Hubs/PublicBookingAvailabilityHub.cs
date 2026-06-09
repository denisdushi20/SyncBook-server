using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SyncBook.Server.Services;



namespace SyncBook.Server.Hubs;



/// <summary>

/// Server-push events to group availability:{businessId}:

/// AvailabilityChanged, BusinessConfigChanged, BusinessStatusChanged.

/// </summary>

[AllowAnonymous]

public class PublicBookingAvailabilityHub : Hub

{

    private const int MaxMessageLength = 2000;



    private readonly SupportChatDispatcher _chatDispatcher;



    public PublicBookingAvailabilityHub(SupportChatDispatcher chatDispatcher)

    {

        _chatDispatcher = chatDispatcher;

    }



    public Task JoinAvailability(string businessId)

    {

        if (string.IsNullOrWhiteSpace(businessId))

        {

            throw new HubException("Business id is required.");

        }



        return Groups.AddToGroupAsync(Context.ConnectionId, $"availability:{businessId}");

    }



    public async Task SendMessageToOwner(

        string businessId,

        string message,

        string visitorConnectionId,

        string visitorSessionId,

        string visitorEmail)

    {

        if (string.IsNullOrWhiteSpace(businessId))

        {

            throw new HubException("Business id is required.");

        }



        if (string.IsNullOrWhiteSpace(visitorSessionId))

        {

            throw new HubException("Visitor session id is required.");

        }



        if (!VisitorEmailHelper.TryNormalize(visitorEmail, out var normalizedEmail, out var emailError))

        {

            throw new HubException(emailError ?? "Invalid visitor email.");

        }



        var trimmed = message?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(trimmed))

        {

            throw new HubException("Message is required.");

        }



        if (trimmed.Length > MaxMessageLength)

        {

            throw new HubException($"Message cannot exceed {MaxMessageLength} characters.");

        }



        if (visitorConnectionId != Context.ConnectionId)

        {

            throw new HubException("Invalid visitor connection id.");

        }



        await _chatDispatcher.DispatchVisitorMessageAsync(
            businessId,
            normalizedEmail,
            visitorSessionId,
            Context.ConnectionId,
            trimmed);
    }
}

