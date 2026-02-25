using ChatApp.Application.Inbox;
using ChatApp.Application.Messages;

namespace ChatApp.Api.Realtime;

public interface IRealtimeNotifier
{
    Task NotifyMessageCreatedAsync(Guid conversationId, MessageDto message, CancellationToken ct = default);
    Task NotifyInboxUpsertsAsync(IReadOnlyList<(Guid UserId, InboxUpsertEvent Event)> upserts, CancellationToken ct = default);
}