namespace ChatApp.Domain.Conversations;

public sealed class ConversationMember
{
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = "member";
    public DateTimeOffset JoinedAt { get; private set; } = DateTimeOffset.UtcNow;
    public Guid? LastReadMessageId { get; set; }          // nebo domain metoda
    public DateTimeOffset? LastReadAt { get; set; }       // idem
    public int UnreadCount { get; set; }                  // idem

    // MVP: pro "read" tracking

    private ConversationMember() { }

    public ConversationMember(Guid conversationId, Guid userId, string role)
    {
        ConversationId = conversationId;
        UserId = userId;
        Role = role.Trim();
    }

    public void MarkRead(Guid messageId) => LastReadMessageId = messageId;
}