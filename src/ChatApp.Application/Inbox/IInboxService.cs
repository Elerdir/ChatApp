using ChatApp.Api.Common;

namespace ChatApp.Application.Inbox;

public interface IInboxService
{
    Task<Result<IReadOnlyList<(Guid UserId, InboxUpsertEvent Event)>>> BuildUpsertsAsync(
        Guid conversationId,
        CancellationToken ct = default);
}