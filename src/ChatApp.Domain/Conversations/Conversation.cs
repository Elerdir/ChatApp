using ChatApp.Domain.Users;

namespace ChatApp.Domain.Conversations;

public sealed class Conversation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public ConversationType Type { get; private set; }
    public string? Title { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private readonly List<ConversationMember> _members = new();
    public IReadOnlyCollection<ConversationMember> Members => _members;

    private readonly List<Message> _messages = new();
    public IReadOnlyCollection<Message> Messages => _messages;

    private Conversation() { }

    public static Conversation CreateDirect() => new() { Type = ConversationType.Direct };
    public static Conversation CreateGroup(string title) => new() { Type = ConversationType.Group, Title = title.Trim() };
    
    public Guid? LastMessageId { get; private set; }
    public DateTimeOffset? LastMessageAt { get; private set; }

    public void AddMember(User user, string role = "member")
    {
        if (_members.Any(m => m.UserId == user.Id)) return;
        _members.Add(new ConversationMember(Id, user.Id, role));
    }
    
    public void SetTitle(string title)
    {
        if (Type != ConversationType.Group)
            throw new InvalidOperationException("Only group conversations can have title.");
        Title = title.Trim();
    }
    
    public void TouchLastMessage(Guid messageId, DateTimeOffset createdAt)
    {
        LastMessageId = messageId;
        LastMessageAt = createdAt;
    }
}