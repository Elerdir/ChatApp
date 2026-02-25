using ChatApp.Api.Common;

namespace ChatApp.Application.Conversations;

public interface IConversationMembershipService
{
    Task<Result<IReadOnlyList<Guid>>> GetConversationIdsForUserAsync(Guid userId, CancellationToken ct = default);
}