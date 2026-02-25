using ChatApp.Api.Common;
using ChatApp.Application.Abstractions;
using ChatApp.Application.Common;
using ChatApp.Domain.Conversations;
using ChatApp.Application.Inbox;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Application.Conversations;

public sealed class ConversationService : IConversationService
{
    private readonly IAppDbContext _db;
    private readonly IEventPublisher _events;

    public ConversationService(IAppDbContext db, IEventPublisher events)
    {
        _db = db;
        _events = events;
    }

    public async Task<Result<IReadOnlyList<InboxItemDto>>> ListMineAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return Result<IReadOnlyList<InboxItemDto>>.Fail(new AppError("auth.unauthorized", "Unauthorized.", ErrorType.Unauthorized));

        limit = Math.Clamp(limit, 1, 100);

        // Rychlý inbox list díky denormalizaci:
        // - conversation_members.unread_count
        // - conversations.last_message_at
        var items = await _db.ConversationMembers
            .Where(m => m.UserId == userId)
            .Join(
                _db.Conversations,
                m => m.ConversationId,
                c => c.Id,
                (m, c) => new { m, c })
            .OrderByDescending(x => x.c.LastMessageAt ?? x.c.CreatedAt)
            .Take(limit)
            .Select(x => new
            {
                x.c.Id,
                x.c.Type,
                x.c.Title,
                x.c.CreatedAt,
                x.c.LastMessageAt,
                x.m.LastReadMessageId,
                x.m.UnreadCount
            })
            .ToListAsync(ct);

        // Abychom uměli DisplayName pro direct, potřebujeme “other user” + jeho displayName.
        // Uděláme to jen pro direct konverzace v listu (typicky jich je hodně, ale pořád OK pro limit 100).
        var directIds = items.Where(i => i.Type == ConversationType.Direct).Select(i => i.Id).ToArray();

        Dictionary<Guid, string> directDisplayNamesByConversationId = new();
        if (directIds.Length > 0)
        {
            // najdi pro každou direct konverzaci userId "toho druhého"
            var otherUserIds = await _db.ConversationMembers
                .Where(m => directIds.Contains(m.ConversationId) && m.UserId != userId)
                .Select(m => new { m.ConversationId, m.UserId })
                .ToListAsync(ct);

            var otherIds = otherUserIds.Select(x => x.UserId).Distinct().ToArray();

            var users = await _db.Users
                .Where(u => otherIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username })
                .ToListAsync(ct);

            var profiles = await _db.UserProfiles
                .Where(p => otherIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.DisplayName })
                .ToListAsync(ct);

            var profileMap = profiles.ToDictionary(x => x.UserId, x => x.DisplayName);
            var userMap = users.ToDictionary(
                x => x.Id,
                x => string.IsNullOrWhiteSpace(profileMap.GetValueOrDefault(x.Id)) ? x.Username : profileMap[x.Id]!);

            foreach (var pair in otherUserIds)
            {
                if (userMap.TryGetValue(pair.UserId, out var dn))
                    directDisplayNamesByConversationId[pair.ConversationId] = dn;
            }
        }

        var result = items.Select(i =>
        {
            var displayName = i.Type == ConversationType.Group
                ? (string.IsNullOrWhiteSpace(i.Title) ? "Group" : i.Title!)
                : (directDisplayNamesByConversationId.TryGetValue(i.Id, out var dn) ? dn : "Direct chat");

            var title = i.Type == ConversationType.Group ? i.Title : null;

            return new InboxItemDto(
                ConversationId: i.Id,
                Type: (int)i.Type,
                DisplayName: displayName,
                Title: title,
                CreatedAt: i.CreatedAt,
                LastMessageAt: i.LastMessageAt,
                LastReadMessageId: i.LastReadMessageId,
                UnreadCount: i.UnreadCount);
        }).ToList();

        return Result<IReadOnlyList<InboxItemDto>>.Ok(result);
    }

    public async Task<Result<ConversationCreatedDto>> CreateDirectAsync(Guid userId, Guid otherUserId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return Result<ConversationCreatedDto>.Fail(new AppError("auth.unauthorized", "Unauthorized.", ErrorType.Unauthorized));

        if (otherUserId == Guid.Empty || otherUserId == userId)
        {
            return Result<ConversationCreatedDto>.Fail(new AppError(
                Code: "conversations.invalid_input",
                Message: "Invalid user id.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["otherUserId"] = new[] { "Must be a different user." }
                }));
        }

        // už existuje direct?
        // (MVP query; ideálně mít v DB unique “direct_key”)
        var existingId = await _db.Conversations
            .Where(c => c.Type == ConversationType.Direct)
            .Where(c => _db.ConversationMembers.Count(m =>
                m.ConversationId == c.Id &&
                (m.UserId == userId || m.UserId == otherUserId)) == 2)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (existingId != Guid.Empty)
        {
            // i když existuje, je fajn vrátit dto
            var existing = await _db.Conversations
                .Where(c => c.Id == existingId)
                .Select(c => new ConversationCreatedDto(
                    c.Id,
                    (int)c.Type,
                    c.Title,
                    new[] { userId, otherUserId },
                    c.CreatedAt))
                .SingleAsync(ct);

            return Result<ConversationCreatedDto>.Ok(existing);
        }

        var me = await _db.Users.FindAsync([userId], ct);
        var other = await _db.Users.FindAsync([otherUserId], ct);

        if (me is null || other is null)
        {
            return Result<ConversationCreatedDto>.Fail(new AppError(
                Code: "users.not_found",
                Message: "User not found.",
                Type: ErrorType.NotFound));
        }

        var convo = Conversation.CreateDirect();
        convo.AddMember(me, role: "member");
        convo.AddMember(other, role: "member");

        _db.Conversations.Add(convo);
        await _db.SaveChangesAsync(ct);

        var created = new ConversationCreatedDto(
            ConversationId: convo.Id,
            Type: (int)convo.Type,
            Title: convo.Title,
            MemberUserIds: new[] { userId, otherUserId },
            CreatedAt: convo.CreatedAt);

        // realtime: conversation.created
        await _events.PublishToUsersAsync(created.MemberUserIds, ConversationEvents.ConversationCreated,
            new ConversationCreatedEvent(created), ct);

        // realtime: inbox.upsert per user (displayName = other user display name)
        var otherDnForMe = await GetDisplayNameAsync(otherUserId, ct);
        var meDnForOther = await GetDisplayNameAsync(userId, ct);

        var meItem = new InboxItemDto(
            ConversationId: convo.Id,
            Type: (int)convo.Type,
            DisplayName: otherDnForMe,
            Title: null,
            CreatedAt: convo.CreatedAt,
            LastMessageAt: convo.LastMessageAt,
            LastReadMessageId: null,
            UnreadCount: 0);

        var otherItem = new InboxItemDto(
            ConversationId: convo.Id,
            Type: (int)convo.Type,
            DisplayName: meDnForOther,
            Title: null,
            CreatedAt: convo.CreatedAt,
            LastMessageAt: convo.LastMessageAt,
            LastReadMessageId: null,
            UnreadCount: 0);

        await _events.PublishToUserAsync(userId, InboxEvents.InboxUpsert, new InboxUpsertEvent(meItem), ct);
        await _events.PublishToUserAsync(otherUserId, InboxEvents.InboxUpsert, new InboxUpsertEvent(otherItem), ct);

        return Result<ConversationCreatedDto>.Ok(created);
    }

    public async Task<Result<ConversationCreatedDto>> CreateGroupAsync(Guid userId, CreateGroupConversationRequest req, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return Result<ConversationCreatedDto>.Fail(new AppError("auth.unauthorized", "Unauthorized.", ErrorType.Unauthorized));

        var title = (req.Title ?? "").Trim();
        if (title.Length is < 1 or > 200)
        {
            return Result<ConversationCreatedDto>.Fail(new AppError(
                Code: "conversations.invalid_title",
                Message: "Invalid title.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["title"] = new[] { "Title must be 1-200 characters." }
                }));
        }

        var memberIds = req.MemberUserIds?.Distinct().Where(x => x != Guid.Empty && x != userId).ToArray() ?? Array.Empty<Guid>();

        // group = aspoň 3 lidi celkem (creator + min 2 další)
        if (memberIds.Length < 2)
        {
            return Result<ConversationCreatedDto>.Fail(new AppError(
                Code: "conversations.group_requires_3",
                Message: "Group must have at least 3 members.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["memberUserIds"] = new[] { "Provide at least 2 additional users." }
                }));
        }

        var allIds = memberIds.Append(userId).Distinct().ToArray();
        var users = await _db.Users.Where(u => allIds.Contains(u.Id)).ToListAsync(ct);

        if (users.Count != allIds.Length)
        {
            return Result<ConversationCreatedDto>.Fail(new AppError(
                Code: "users.not_found",
                Message: "One or more users not found.",
                Type: ErrorType.NotFound));
        }

        var convo = Conversation.CreateGroup(title);
        foreach (var u in users)
            convo.AddMember(u, role: u.Id == userId ? "owner" : "member");

        _db.Conversations.Add(convo);
        await _db.SaveChangesAsync(ct);

        var created = new ConversationCreatedDto(
            ConversationId: convo.Id,
            Type: (int)convo.Type,
            Title: convo.Title,
            MemberUserIds: users.Select(x => x.Id).ToArray(),
            CreatedAt: convo.CreatedAt);

        await _events.PublishToUsersAsync(created.MemberUserIds, ConversationEvents.ConversationCreated,
            new ConversationCreatedEvent(created), ct);

        var item = new InboxItemDto(
            ConversationId: convo.Id,
            Type: (int)convo.Type,
            DisplayName: string.IsNullOrWhiteSpace(convo.Title) ? "Group" : convo.Title!,
            Title: convo.Title,
            CreatedAt: convo.CreatedAt,
            LastMessageAt: convo.LastMessageAt,
            LastReadMessageId: null,
            UnreadCount: 0);

        await _events.PublishToUsersAsync(created.MemberUserIds, InboxEvents.InboxUpsert, new InboxUpsertEvent(item), ct);

        return Result<ConversationCreatedDto>.Ok(created);
    }

    public async Task<Result<ConversationDto>> GetAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
    {
        var member = await _db.ConversationMembers
            .SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId, ct);

        if (member is null)
        {
            return Result<ConversationDto>.Fail(new AppError(
                Code: "conversations.not_member",
                Message: "You are not a member of this conversation.",
                Type: ErrorType.Forbidden));
        }

        var convo = await _db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId, ct);
        if (convo is null)
        {
            return Result<ConversationDto>.Fail(new AppError(
                Code: "conversations.not_found",
                Message: "Conversation not found.",
                Type: ErrorType.NotFound));
        }

        var members = await _db.ConversationMembers
            .Where(m => m.ConversationId == conversationId)
            .Select(m => new ConversationMemberDto(m.UserId, m.Role, m.JoinedAt))
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(ct);

        return Result<ConversationDto>.Ok(new ConversationDto(
            ConversationId: convo.Id,
            Type: (int)convo.Type,
            Title: convo.Title,
            CreatedAt: convo.CreatedAt,
            Members: members));
    }

    public async Task<Result> RenameAsync(Guid userId, Guid conversationId, RenameConversationRequest req, CancellationToken ct = default)
    {
        var title = (req.Title ?? "").Trim();
        if (title.Length is < 1 or > 200)
        {
            return Result.Fail(new AppError(
                Code: "conversations.invalid_title",
                Message: "Invalid title.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["title"] = new[] { "Title must be 1-200 characters." }
                }));
        }

        var member = await _db.ConversationMembers
            .SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId, ct);

        if (member is null)
            return Result.Fail(new AppError("conversations.not_member", "You are not a member of this conversation.", ErrorType.Forbidden));

        if (member.Role is not ("owner" or "admin"))
            return Result.Fail(new AppError("conversations.not_allowed", "You are not allowed to rename this conversation.", ErrorType.Forbidden));

        var convo = await _db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId, ct);
        if (convo is null)
            return Result.Fail(new AppError("conversations.not_found", "Conversation not found.", ErrorType.NotFound));

        if (convo.Type == ConversationType.Direct)
            return Result.Fail(new AppError("conversations.cannot_rename_direct", "Direct conversation cannot be renamed.", ErrorType.Conflict));

        convo.SetTitle(title);
        await _db.SaveChangesAsync(ct);

        // optional: inbox upsert (group title changed)
        var members = await _db.ConversationMembers.Where(m => m.ConversationId == conversationId).Select(m => m.UserId).ToArrayAsync(ct);
        var item = new InboxItemDto(convo.Id, (int)convo.Type, title, title, convo.CreatedAt, convo.LastMessageAt, null, 0);
        await _events.PublishToUsersAsync(members, InboxEvents.InboxUpsert, new InboxUpsertEvent(item), ct);

        return Result.Ok();
    }

    public async Task<Result> AddMembersAsync(Guid actorUserId, Guid conversationId, Guid[] memberUserIds, CancellationToken ct = default)
    {
        var actor = await _db.ConversationMembers
            .SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == actorUserId, ct);

        if (actor is null)
            return Result.Fail(new AppError("conversations.not_member", "You are not a member of this conversation.", ErrorType.Forbidden));

        if (actor.Role is not ("owner" or "admin"))
            return Result.Fail(new AppError("conversations.not_allowed", "You are not allowed to add members.", ErrorType.Forbidden));

        var convo = await _db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId, ct);
        if (convo is null)
            return Result.Fail(new AppError("conversations.not_found", "Conversation not found.", ErrorType.NotFound));

        if (convo.Type == ConversationType.Direct)
            return Result.Fail(new AppError("conversations.cannot_add_to_direct", "Cannot add members to a direct conversation.", ErrorType.Conflict));

        var ids = memberUserIds?.Distinct().Where(x => x != Guid.Empty).ToArray() ?? Array.Empty<Guid>();
        if (ids.Length == 0)
        {
            return Result.Fail(new AppError(
                "conversations.invalid_input",
                "Invalid input.",
                ErrorType.Validation,
                new Dictionary<string, string[]>
                {
                    ["memberUserIds"] = new[] { "Provide at least one user id." }
                }));
        }

        var users = await _db.Users.Where(u => ids.Contains(u.Id)).ToListAsync(ct);
        if (users.Count != ids.Length)
            return Result.Fail(new AppError("users.not_found", "One or more users not found.", ErrorType.NotFound));

        foreach (var u in users)
            convo.AddMember(u, role: "member");

        await _db.SaveChangesAsync(ct);

        // optional: notify newly added members with conversation.created + inbox upsert
        // (můžeš doplnit podle potřeby)

        return Result.Ok();
    }

    public async Task<Result> RemoveMemberAsync(Guid actorUserId, Guid conversationId, Guid memberUserId, CancellationToken ct = default)
    {
        var actor = await _db.ConversationMembers
            .SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == actorUserId, ct);

        if (actor is null)
            return Result.Fail(new AppError("conversations.not_member", "You are not a member of this conversation.", ErrorType.Forbidden));

        if (actor.Role is not ("owner" or "admin"))
            return Result.Fail(new AppError("conversations.not_allowed", "You are not allowed to remove members.", ErrorType.Forbidden));

        if (actorUserId == memberUserId)
            return Result.Fail(new AppError("conversations.invalid_input", "Use /leave to leave the conversation.", ErrorType.Conflict));

        var member = await _db.ConversationMembers
            .SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == memberUserId, ct);

        if (member is null)
            return Result.Fail(new AppError("conversations.member_not_found", "Member not found in this conversation.", ErrorType.NotFound));

        if (member.Role == "owner")
            return Result.Fail(new AppError("conversations.cannot_remove_owner", "Owner cannot be removed.", ErrorType.Conflict));

        _db.ConversationMembers.Remove(member);
        await _db.SaveChangesAsync(ct);

        // optional: notify removed user inbox.remove
        await _events.PublishToUserAsync(memberUserId, InboxEvents.InboxRemove, new InboxRemoveEvent(conversationId), ct);

        return Result.Ok();
    }

    public async Task<Result> LeaveAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
    {
        var member = await _db.ConversationMembers
            .SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId, ct);

        if (member is null)
            return Result.Fail(new AppError("conversations.not_member", "You are not a member of this conversation.", ErrorType.Forbidden));

        if (member.Role == "owner")
            return Result.Fail(new AppError("conversations.owner_cannot_leave", "Owner cannot leave without transferring ownership.", ErrorType.Conflict));

        _db.ConversationMembers.Remove(member);
        await _db.SaveChangesAsync(ct);

        await _events.PublishToUserAsync(userId, InboxEvents.InboxRemove, new InboxRemoveEvent(conversationId), ct);

        return Result.Ok();
    }

    // ---------------- helpers ----------------

    private async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken ct)
    {
        var p = await _db.UserProfiles.FindAsync([userId], ct);
        if (p is not null && !string.IsNullOrWhiteSpace(p.DisplayName))
            return p.DisplayName;

        var u = await _db.Users.FindAsync([userId], ct);
        return u?.Username ?? "Unknown";
    }
}