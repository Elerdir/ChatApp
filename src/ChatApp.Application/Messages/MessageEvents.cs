namespace ChatApp.Application.Messages;

public static class MessageEvents
{
    public const string MessageCreated = "message.created";
    public const string MessageRead = "message.read";
}

public sealed record MessageCreatedEvent(MessageDto Message);
public sealed record MessageReadEvent(Guid ConversationId, Guid UserId, Guid LastReadMessageId);