using ChatApp.Api.Common;
using ChatApp.Application.Abstractions;
using ChatApp.Application.Common;
using ChatApp.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Application.Messages;

public sealed class MessageService : IMessageService
{
    private readonly IAppDbContext _db;

    public MessageService(IAppDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<MessageDto>>> GetHistoryAsync(
        Guid userId,
        Guid conversationId,
        DateTimeOffset? before,
        int limit,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return Result<IReadOnlyList<MessageDto>>.Fail(new AppError("auth.unauthorized", "Unauthorized.", ErrorType.Unauthorized));

        if (conversationId == Guid.Empty)
            return Result<IReadOnlyList<MessageDto>>.Fail(new AppError("messages.invalid_conversation_id", "Invalid conversation id.", ErrorType.Validation));

        limit = Math.Clamp(limit, 1, 100);

        var isMember = await _db.ConversationMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId, ct);

        if (!isMember)
        {
            return Result<IReadOnlyList<MessageDto>>.Fail(new AppError(
                Code: "conversations.not_member",
                Message: "You are not a member of this conversation.",
                Type: ErrorType.Forbidden));
        }

        var query = _db.Messages.Where(m => m.ConversationId == conversationId);

        if (before is not null)
            query = query.Where(m => m.CreatedAt < before.Value);

        var msgs = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(m => new MessageDto(
                MessageId: m.Id,
                ConversationId: m.ConversationId,
                SenderId: m.SenderId,
                Body: m.Body,
                CreatedAt: m.CreatedAt,
                ClientMessageId: m.ClientMessageId))
            .ToListAsync(ct);

        msgs.Reverse();
        return Result<IReadOnlyList<MessageDto>>.Ok(msgs);
    }

    public async Task<Result<MessageDto>> SendAsync(
        Guid userId,
        Guid deviceId,
        Guid conversationId,
        SendMessageRequest req,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return Result<MessageDto>.Fail(new AppError("auth.unauthorized", "Unauthorized.", ErrorType.Unauthorized));

        if (conversationId == Guid.Empty)
            return Result<MessageDto>.Fail(new AppError("messages.invalid_conversation_id", "Invalid conversation id.", ErrorType.Validation));

        var body = (req.Body ?? "").Trim();
        if (body.Length < 1 || body.Length > 8000)
        {
            return Result<MessageDto>.Fail(new AppError(
                Code: "messages.invalid_body",
                Message: "Invalid message body.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["body"] = new[] { "Body must be 1-8000 characters." }
                }));
        }

        if (req.ClientMessageId == Guid.Empty)
        {
            return Result<MessageDto>.Fail(new AppError(
                Code: "messages.client_message_id_required",
                Message: "ClientMessageId is required.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["clientMessageId"] = new[] { "ClientMessageId is required." }
                }));
        }

        var member = await _db.ConversationMembers
            .SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId, ct);

        if (member is null)
        {
            return Result<MessageDto>.Fail(new AppError(
                Code: "conversations.not_member",
                Message: "You are not a member of this conversation.",
                Type: ErrorType.Forbidden));
        }

        // Idempotence: same clientMessageId from same sender in same conversation
        var existing = await _db.Messages
            .SingleOrDefaultAsync(m =>
                m.ConversationId == conversationId &&
                m.SenderId == userId &&
                m.ClientMessageId == req.ClientMessageId, ct);

        if (existing is not null)
        {
            // (volitelně) můžeš i syncnout last_message_* zde, ale typicky není potřeba
            return Result<MessageDto>.Ok(ToDto(existing));
        }

        // Create
        var msg = new Message(conversationId, userId, body, req.ClientMessageId);
        _db.Messages.Add(msg);

        // Load conversation (for last_message_*)
        var convo = await _db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId, ct);
        if (convo is null)
        {
            return Result<MessageDto>.Fail(new AppError(
                Code: "conversations.not_found",
                Message: "Conversation not found.",
                Type: ErrorType.NotFound));
        }

        // Update denormalized conversation fields
        convo.TouchLastMessage(msg.Id, msg.CreatedAt);

        // Sender reads his own message immediately
        member.LastReadMessageId = msg.Id;
        member.LastReadAt = msg.CreatedAt;
        member.UnreadCount = 0;

        // Persist message + convo + sender updates
        await _db.SaveChangesAsync(ct);

        // Increment unread for everyone else
        // Preferred: single SQL UPDATE (EF Core ExecuteUpdateAsync)
        try
        {
            await _db.ConversationMembers
                .Where(m => m.ConversationId == conversationId && m.UserId != userId)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(m => m.UnreadCount, m => m.UnreadCount + 1), ct);
        }
        catch
        {
            // Fallback if ExecuteUpdateAsync isn't available/configured
            var others = await _db.ConversationMembers
                .Where(m => m.ConversationId == conversationId && m.UserId != userId)
                .ToListAsync(ct);

            foreach (var o in others)
                o.UnreadCount += 1;

            await _db.SaveChangesAsync(ct);
        }

        return Result<MessageDto>.Ok(ToDto(msg));
    }

    public async Task<Result> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        MarkReadRequest req,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return Result.Fail(new AppError("auth.unauthorized", "Unauthorized.", ErrorType.Unauthorized));

        if (conversationId == Guid.Empty)
            return Result.Fail(new AppError("messages.invalid_conversation_id", "Invalid conversation id.", ErrorType.Validation));

        if (req.LastReadMessageId == Guid.Empty)
        {
            return Result.Fail(new AppError(
                Code: "messages.invalid_last_read",
                Message: "LastReadMessageId is required.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["lastReadMessageId"] = new[] { "LastReadMessageId is required." }
                }));
        }

        var member = await _db.ConversationMembers
            .SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId, ct);

        if (member is null)
        {
            return Result.Fail(new AppError(
                Code: "conversations.not_member",
                Message: "You are not a member of this conversation.",
                Type: ErrorType.Forbidden));
        }

        // Fetch message createdAt and ensure it belongs to convo
        var msg = await _db.Messages
            .Where(m => m.Id == req.LastReadMessageId && m.ConversationId == conversationId)
            .Select(m => new { m.Id, m.CreatedAt })
            .SingleOrDefaultAsync(ct);

        if (msg is null)
        {
            return Result.Fail(new AppError(
                Code: "messages.not_found",
                Message: "Message not found in this conversation.",
                Type: ErrorType.NotFound));
        }

        // If already read newer/equal, keep as is (optional)
        // You can compare by LastReadAt or by message time if you want monotonicity.
        member.LastReadMessageId = msg.Id;
        member.LastReadAt = msg.CreatedAt;
        member.UnreadCount = 0;

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static MessageDto ToDto(Message m) => new(
        MessageId: m.Id,
        ConversationId: m.ConversationId,
        SenderId: m.SenderId,
        Body: m.Body,
        CreatedAt: m.CreatedAt,
        ClientMessageId: m.ClientMessageId);
}