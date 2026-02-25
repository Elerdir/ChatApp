using System.Security.Claims;
using System.Text.Json;
using ChatApp.Api.Common;
using ChatApp.Application.Conversations;
using ChatApp.Application.Inbox;
using ChatApp.Application.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.Api.Realtime;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IConversationMembershipService _membership;
    private readonly IMessageService _messages;
    private readonly IInboxService _inbox;
    private readonly HubRateLimiter _rateLimiter;

    public ChatHub(
        IConversationMembershipService membership,
        IMessageService messages,
        IInboxService inbox,
        HubRateLimiter rateLimiter)
    {
        _membership = membership;
        _messages = messages;
        _inbox = inbox;
        _rateLimiter = rateLimiter;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetUserId();

        var res = await _membership.GetConversationIdsForUserAsync(userId, Context.ConnectionAborted);
        if (!res.IsSuccess)
            throw new HubException(ToHubErrorJson(res.Error!.Code, res.Error!.Message));

        foreach (var convoId in res.Value!)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GroupName(convoId),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = Context.User!.GetUserId();

        var res = await _membership.GetConversationIdsForUserAsync(userId, Context.ConnectionAborted);
        if (!res.IsSuccess)
            throw new HubException(ToHubErrorJson(res.Error!.Code, res.Error!.Message));

        if (!res.Value!.Contains(conversationId))
            throw new HubException(ToHubErrorJson("conversations.not_member", "You are not a member of this conversation."));

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId), Context.ConnectionAborted);
    }

    public async Task<MessageDto> SendMessage(Guid conversationId, SendMessageRequest req)
    {
        var userId = Context.User!.GetUserId();
        var deviceId = Context.User!.GetDeviceId();

        // Anti-spam (per user + conversation)
        var limiterKey = $"{userId:N}:{conversationId:N}";
        var lease = _rateLimiter.TryAcquire(limiterKey, permits: 1);

        if (!lease.IsAcquired)
            throw new HubException(ToHubErrorJson("rate_limited", "Too many messages. Slow down."));

        // Persist + denorm updates in service
        var res = await _messages.SendAsync(userId, deviceId, conversationId, req, Context.ConnectionAborted);
        if (!res.IsSuccess)
            throw new HubException(ToHubErrorJson(res.Error!.Code, res.Error!.Message));

        var msg = res.Value!;

        // Broadcast message to everyone in conversation group
        await Clients.Group(GroupName(conversationId))
            .SendAsync(MessageEvents.MessageCreated, new MessageCreatedEvent(msg), Context.ConnectionAborted);

        // Push inbox upsert to all members (DisplayName included by InboxService)
        var upserts = await _inbox.BuildUpsertsAsync(conversationId, Context.ConnectionAborted);
        if (upserts.IsSuccess)
        {
            foreach (var (uid, ev) in upserts.Value!)
            {
                await Clients.User(uid.ToString())
                    .SendAsync(InboxEvents.InboxUpsert, ev, Context.ConnectionAborted);
            }
        }

        return msg; // ack for sender
    }

    public async Task MarkRead(Guid conversationId, MarkReadRequest req)
    {
        var userId = Context.User!.GetUserId();

        var res = await _messages.MarkReadAsync(userId, conversationId, req, Context.ConnectionAborted);
        if (!res.IsSuccess)
            throw new HubException(ToHubErrorJson(res.Error!.Code, res.Error!.Message));

        await Clients.Group(GroupName(conversationId))
            .SendAsync(MessageEvents.MessageRead,
                new MessageReadEvent(conversationId, userId, req.LastReadMessageId),
                Context.ConnectionAborted);

        // inbox update just for me (UnreadCount=0)
        var upserts = await _inbox.BuildUpsertsAsync(conversationId, Context.ConnectionAborted);
        if (upserts.IsSuccess)
        {
            var mine = upserts.Value!.FirstOrDefault(x => x.UserId == userId);
            if (mine != default)
            {
                await Clients.User(userId.ToString())
                    .SendAsync(InboxEvents.InboxUpsert, mine.Event, Context.ConnectionAborted);
            }
        }
    }

    private static string GroupName(Guid conversationId) => $"convo:{conversationId:N}";

    private static string ToHubErrorJson(string code, string message)
        => JsonSerializer.Serialize(new HubError(code, message));
}