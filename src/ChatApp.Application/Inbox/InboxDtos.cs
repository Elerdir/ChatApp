namespace ChatApp.Application.Inbox;

public sealed record InboxItemDto(
    Guid ConversationId,
    int Type,                 // 1=Direct, 2=Group
    string DisplayName,       // co se má ukazovat v inboxu
    string? Title,            // volitelné: u group může být stejné jako DisplayName, u direct klidně null
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    Guid? LastReadMessageId,
    int UnreadCount);