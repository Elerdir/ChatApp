namespace ChatApp.Application.Conversations;

public static class ConversationEvents
{
    public const string ConversationCreated = "conversation.created";
}

public sealed record ConversationCreatedEvent(ConversationCreatedDto Conversation);

public sealed record ConversationCreatedDto(
    Guid ConversationId,
    int Type,
    string? Title,
    Guid[] MemberUserIds,
    DateTimeOffset CreatedAt);