using ChatApp.Api.Common;
using ChatApp.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Application.Conversations;

public sealed class ConversationMembershipService : IConversationMembershipService
{
    private readonly IAppDbContext _db;
    public ConversationMembershipService(IAppDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<Guid>>> GetConversationIdsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var ids = await _db.ConversationMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ConversationId)
            .ToListAsync(ct);

        return Result<IReadOnlyList<Guid>>.Ok(ids);
    }
}