namespace ChatApp.Domain.Conversations;

public sealed class Message
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Body { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Idempotence: client pošle svůj GUID, server si ho uloží
    public Guid ClientMessageId { get; private set; }

    private Message() { }

    public Message(Guid conversationId, Guid senderId, string body, Guid clientMessageId)
    {
        ConversationId = conversationId;
        SenderId = senderId;
        Body = body.Trim();
        ClientMessageId = clientMessageId;
    }
}