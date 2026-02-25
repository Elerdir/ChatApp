using ChatApp.Application.Inbox;
using ChatApp.Application.Messages;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.Api.Realtime;

public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _hub;

    public SignalRRealtimeNotifier(IHubContext<ChatHub> hub) => _hub = hub;

    public Task NotifyMessageCreatedAsync(Guid conversationId, MessageDto message, CancellationToken ct = default)
    {
        return _hub.Clients.Group($"convo:{conversationId:N}")
            .SendAsync(MessageEvents.MessageCreated, new MessageCreatedEvent(message), ct);
    }

    public async Task NotifyInboxUpsertsAsync(IReadOnlyList<(Guid UserId, InboxUpsertEvent Event)> upserts, CancellationToken ct = default)
    {
        foreach (var (userId, ev) in upserts)
        {
            await _hub.Clients.User(userId.ToString())
                .SendAsync(InboxEvents.InboxUpsert, ev, ct);
        }
    }
}