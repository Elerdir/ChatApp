using ChatApp.Api.Common;
using ChatApp.Application.Abstractions;
using ChatApp.Application.Common;
using ChatApp.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Application.Inbox;

public sealed class InboxService : IInboxService
{
    private readonly IAppDbContext _db;

    public InboxService(IAppDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<(Guid UserId, InboxUpsertEvent Event)>>> BuildUpsertsAsync(
        Guid conversationId,
        CancellationToken ct = default)
    {
        if (conversationId == Guid.Empty)
        {
            return Result<IReadOnlyList<(Guid, InboxUpsertEvent)>>.Fail(new AppError(
                Code: "inbox.invalid_conversation_id",
                Message: "Invalid conversation id.",
                Type: ErrorType.Validation));
        }

        var convo = await _db.Conversations
            .Where(c => c.Id == conversationId)
            .Select(c => new
            {
                c.Id,
                c.Type,
                c.Title,
                c.CreatedAt,
                c.LastMessageAt
            })
            .SingleOrDefaultAsync(ct);

        if (convo is null)
        {
            return Result<IReadOnlyList<(Guid, InboxUpsertEvent)>>.Fail(new AppError(
                Code: "conversations.not_found",
                Message: "Conversation not found.",
                Type: ErrorType.NotFound));
        }

        var members = await _db.ConversationMembers
            .Where(m => m.ConversationId == conversationId)
            .Select(m => new
            {
                m.UserId,
                m.LastReadMessageId,
                m.UnreadCount
            })
            .ToListAsync(ct);

        if (members.Count == 0)
        {
            return Result<IReadOnlyList<(Guid, InboxUpsertEvent)>>.Fail(new AppError(
                Code: "conversations.not_found",
                Message: "Conversation not found.",
                Type: ErrorType.NotFound));
        }

        // Pro direct potřebujeme displayName druhého uživatele -> načteme profily (a fallback na username)
        Dictionary<Guid, (string Username, string? DisplayName)> userInfo = new();

        if (convo.Type == ConversationType.Direct)
        {
            var memberIds = members.Select(x => x.UserId).Distinct().ToArray();

            var users = await _db.Users
                .Where(u => memberIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username })
                .ToListAsync(ct);

            var profiles = await _db.UserProfiles
                .Where(p => memberIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.DisplayName })
                .ToListAsync(ct);

            var profileMap = profiles.ToDictionary(x => x.UserId, x => x.DisplayName);
            userInfo = users.ToDictionary(
                u => u.Id,
                u => (u.Username, profileMap.TryGetValue(u.Id, out var dn) ? dn : null));
        }

        var list = new List<(Guid, InboxUpsertEvent)>(members.Count);

        foreach (var m in members)
        {
            string displayName;

            if (convo.Type == ConversationType.Group)
            {
                displayName = string.IsNullOrWhiteSpace(convo.Title) ? "Group" : convo.Title!;
            }
            else // Direct
            {
                // najdi "druhého" člena
                var otherUserId = members.FirstOrDefault(x => x.UserId != m.UserId)?.UserId ?? Guid.Empty;

                if (otherUserId != Guid.Empty && userInfo.TryGetValue(otherUserId, out var info))
                    displayName = string.IsNullOrWhiteSpace(info.DisplayName) ? info.Username : info.DisplayName!;
                else
                    displayName = "Direct chat";
            }

            var title = convo.Type == ConversationType.Group ? convo.Title : null;

            var item = new InboxItemDto(
                ConversationId: convo.Id,
                Type: (int)convo.Type,
                DisplayName: displayName,
                Title: title,
                CreatedAt: convo.CreatedAt,
                LastMessageAt: convo.LastMessageAt,
                LastReadMessageId: m.LastReadMessageId,
                UnreadCount: m.UnreadCount);

            list.Add((m.UserId, new InboxUpsertEvent(item)));
        }

        return Result<IReadOnlyList<(Guid, InboxUpsertEvent)>>.Ok(list);
    }
}