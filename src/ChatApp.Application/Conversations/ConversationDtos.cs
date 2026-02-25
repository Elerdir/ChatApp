namespace ChatApp.Application.Conversations;

public sealed record ConversationListItemDto(
    Guid ConversationId,
    int Type,                 // 1=Direct, 2=Group (nebo string)
    string? Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    Guid? LastReadMessageId);

public sealed record ConversationMemberDto(Guid UserId, string Role, DateTimeOffset JoinedAt);

public sealed record ConversationDto(
    Guid ConversationId,
    int Type,
    string? Title,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ConversationMemberDto> Members);

public sealed record CreateDirectConversationRequest(Guid OtherUserId);

public sealed record CreateGroupConversationRequest(string Title, Guid[] MemberUserIds);

public sealed record RenameConversationRequest(string Title);

public sealed record AddMembersRequest(Guid[] MemberUserIds);