using System.Security.Claims;
using ChatApp.Api.Common;
using ChatApp.Api.Filters;
using ChatApp.Api.Logging;
using ChatApp.Api.Realtime;
using ChatApp.Application.Inbox;
using ChatApp.Application.Messages;

namespace ChatApp.Api.Endpoints;

public static class MessageEndpoints
{
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/conversations/{conversationId:guid}/messages")
            .WithTags("Messages")
            .RequireAuthorization();

        // historie zpráv
        group.MapGet("/", GetHistory);

        // odeslání zprávy (REST) + anti-spam + validace
        group.MapPost("/", Send)
            .RequireRateLimiting("send-message")
            .AddEndpointFilter<ValidateBodyFilter<SendMessageRequest>>();

        // mark read
        group.MapPost("/read", MarkRead)
            .AddEndpointFilter<ValidateBodyFilter<MarkReadRequest>>();

        return app;
    }

    private static async Task<IResult> GetHistory(
        HttpContext ctx,
        ClaimsPrincipal me,
        Guid conversationId,
        DateTimeOffset? before,
        int limit,
        IMessageService messages,
        CancellationToken ct)
    {
        var userId = me.GetUserId();

        limit = limit <= 0 ? 50 : Math.Clamp(limit, 1, 100);

        var res = await messages.GetHistoryAsync(userId, conversationId, before, limit, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> Send(
        HttpContext ctx,
        ClaimsPrincipal me,
        Guid conversationId,
        SendMessageRequest req,
        IMessageService messages,
        IInboxService inbox,
        IRealtimeNotifier realtime,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var deviceId = me.GetDeviceId();

        Audit.Info("message.send.attempt", new
        {
            userId,
            deviceId,
            conversationId
        });

        var res = await messages.SendAsync(userId, deviceId, conversationId, req, ct);
        if (!res.IsSuccess)
        {
            Audit.Warn("message.send.failed", new
            {
                userId,
                deviceId,
                conversationId,
                code = res.Error!.Code
            });

            return res.ToHttp(ctx);
        }

        var msg = res.Value!;

        Audit.Info("message.send.success", new
        {
            userId,
            deviceId,
            conversationId,
            messageId = msg.MessageId
        });

        // REST -> realtime: message.created
        await realtime.NotifyMessageCreatedAsync(conversationId, msg, ct);

        // REST -> realtime: inbox.upsert (DisplayName included)
        var upserts = await inbox.BuildUpsertsAsync(conversationId, ct);
        if (upserts.IsSuccess)
        {
            await realtime.NotifyInboxUpsertsAsync(upserts.Value!, ct);
        }

        return Results.Ok(msg);
    }

    private static async Task<IResult> MarkRead(
        HttpContext ctx,
        ClaimsPrincipal me,
        Guid conversationId,
        MarkReadRequest req,
        IMessageService messages,
        IInboxService inbox,
        IRealtimeNotifier realtime,
        CancellationToken ct)
    {
        var userId = me.GetUserId();

        Audit.Info("message.mark_read.attempt", new
        {
            userId,
            conversationId,
            lastReadMessageId = req.LastReadMessageId
        });

        var res = await messages.MarkReadAsync(userId, conversationId, req, ct);
        if (!res.IsSuccess)
        {
            Audit.Warn("message.mark_read.failed", new
            {
                userId,
                conversationId,
                code = res.Error!.Code
            });

            return res.ToHttp(ctx);
        }

        Audit.Info("message.mark_read.success", new
        {
            userId,
            conversationId,
            lastReadMessageId = req.LastReadMessageId
        });

        // Realtime: inbox upsert jen pro mě (UnreadCount=0)
        var upserts = await inbox.BuildUpsertsAsync(conversationId, ct);
        if (upserts.IsSuccess)
        {
            var mine = upserts.Value!.FirstOrDefault(x => x.UserId == userId);
            if (mine != default)
                await realtime.NotifyInboxUpsertsAsync(new[] { mine }, ct);
        }

        return Results.NoContent();
    }
}