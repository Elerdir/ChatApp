namespace ChatApp.Application.Inbox;

public static class InboxEvents
{
    public const string InboxUpsert = "inbox.upsert";
    public const string InboxRemove = "inbox.remove";
}

public sealed record InboxUpsertEvent(InboxItemDto Item);
public sealed record InboxRemoveEvent(Guid ConversationId);