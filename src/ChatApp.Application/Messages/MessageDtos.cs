namespace ChatApp.Application.Messages;

public sealed record MessageDto(
    Guid MessageId,
    Guid ConversationId,
    Guid SenderId,
    string Body,
    DateTimeOffset CreatedAt,
    Guid ClientMessageId);

public sealed record SendMessageRequest(
    string Body,
    Guid ClientMessageId);

public sealed record MarkReadRequest(
    Guid LastReadMessageId);