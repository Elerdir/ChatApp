using ChatApp.Api.Common;
using ChatApp.Application.Inbox;

namespace ChatApp.Application.Conversations;

public interface IConversationService
{
    Task<Result<IReadOnlyList<InboxItemDto>>> ListMineAsync(Guid userId, int limit, CancellationToken ct = default);

    Task<Result<ConversationCreatedDto>> CreateDirectAsync(Guid userId, Guid otherUserId, CancellationToken ct = default);
    Task<Result<ConversationCreatedDto>> CreateGroupAsync(Guid userId, CreateGroupConversationRequest req, CancellationToken ct = default);

    Task<Result<ConversationDto>> GetAsync(Guid userId, Guid conversationId, CancellationToken ct = default);

    Task<Result> RenameAsync(Guid userId, Guid conversationId, RenameConversationRequest req, CancellationToken ct = default);

    Task<Result> AddMembersAsync(Guid actorUserId, Guid conversationId, Guid[] memberUserIds, CancellationToken ct = default);

    Task<Result> RemoveMemberAsync(Guid actorUserId, Guid conversationId, Guid memberUserId, CancellationToken ct = default);

    Task<Result> LeaveAsync(Guid userId, Guid conversationId, CancellationToken ct = default);
}