using System.Security.Claims;
using ChatApp.Api.Common;
using ChatApp.Api.Filters;
using ChatApp.Api.Logging;
using ChatApp.Application.Abstractions;
using ChatApp.Application.Conversations;
using ChatApp.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Api.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/conversations")
            .WithTags("Conversations")
            .RequireAuthorization();

        // list inbox/conversations (denormalized)
        group.MapGet("/", ListMine);

        // get conversation detail (members etc.)
        group.MapGet("/{conversationId:guid}", Get);

        // create direct
        group.MapPost("/direct", CreateDirect)
            .AddEndpointFilter<ValidateBodyFilter<CreateDirectConversationRequest>>();
            // .RequireRateLimiting("conversations-create"); // volitelné

        // create group
        group.MapPost("/group", CreateGroup)
            .AddEndpointFilter<ValidateBodyFilter<CreateGroupConversationRequest>>();
            // .RequireRateLimiting("conversations-create"); // volitelné

        // rename group
        group.MapPut("/{conversationId:guid}/title", Rename)
            .AddEndpointFilter<ValidateBodyFilter<RenameConversationRequest>>();
            // .RequireRateLimiting("conversations-rename"); // volitelné

        // add members
        group.MapPost("/{conversationId:guid}/members", AddMembers)
            .AddEndpointFilter<ValidateBodyFilter<AddMembersRequest>>();

        // remove member
        group.MapDelete("/{conversationId:guid}/members/{memberUserId:guid}", RemoveMember);

        // leave conversation
        group.MapPost("/{conversationId:guid}/leave", Leave);

        return app;
    }

    private static async Task<IResult> ListMine(
        HttpContext ctx,
        ClaimsPrincipal me,
        int limit,
        IConversationService conversations,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        limit = limit <= 0 ? 50 : Math.Clamp(limit, 1, 100);

        var res = await conversations.ListMineAsync(userId, limit, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> Get(
        HttpContext ctx,
        ClaimsPrincipal me,
        Guid conversationId,
        IConversationService conversations,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var res = await conversations.GetAsync(userId, conversationId, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> CreateDirect(
        HttpContext ctx,
        ClaimsPrincipal me,
        CreateDirectConversationRequest req,
        IConversationService conversations,
        CancellationToken ct)
    {
        var userId = me.GetUserId();

        Audit.Info("conversation.create_direct.attempt", new
        {
            userId,
            otherUserId = req.OtherUserId
        });

        var res = await conversations.CreateDirectAsync(userId, req.OtherUserId, ct);
        if (!res.IsSuccess)
        {
            Audit.Warn("conversation.create_direct.failed", new
            {
                userId,
                otherUserId = req.OtherUserId,
                code = res.Error!.Code
            });
            return res.ToHttp(ctx);
        }

        Audit.Info("conversation.create_direct.success", new
        {
            userId,
            conversationId = res.Value!.ConversationId,
            otherUserId = req.OtherUserId
        });

        return Results.Ok(res.Value);
    }

    private static async Task<IResult> CreateGroup(
        HttpContext ctx,
        ClaimsPrincipal me,
        CreateGroupConversationRequest req,
        IConversationService conversations,
        CancellationToken ct)
    {
        var userId = me.GetUserId();

        Audit.Info("conversation.create_group.attempt", new
        {
            userId,
            memberCount = req.MemberUserIds?.Distinct().Count() ?? 0,
            title = req.Title
        });

        var res = await conversations.CreateGroupAsync(userId, req, ct);
        if (!res.IsSuccess)
        {
            Audit.Warn("conversation.create_group.failed", new
            {
                userId,
                code = res.Error!.Code
            });
            return res.ToHttp(ctx);
        }

        Audit.Info("conversation.create_group.success", new
        {
            userId,
            conversationId = res.Value!.ConversationId
        });

        return Results.Ok(res.Value);
    }

    private static async Task<IResult> Rename(
        HttpContext ctx,
        ClaimsPrincipal me,
        Guid conversationId,
        RenameConversationRequest req,
        IConversationService conversations,
        CancellationToken ct)
    {
        var userId = me.GetUserId();

        Audit.Info("conversation.rename.attempt", new
        {
            userId,
            conversationId,
            title = req.Title
        });

        var res = await conversations.RenameAsync(userId, conversationId, req, ct);
        if (!res.IsSuccess)
        {
            Audit.Warn("conversation.rename.failed", new
            {
                userId,
                conversationId,
                code = res.Error!.Code
            });
            return res.ToHttp(ctx);
        }

        Audit.Info("conversation.rename.success", new
        {
            userId,
            conversationId
        });

        return Results.NoContent();
    }

    private static async Task<IResult> AddMembers(
        HttpContext ctx,
        ClaimsPrincipal me,
        Guid conversationId,
        AddMembersRequest req,
        IConversationService conversations,
        CancellationToken ct)
    {
        var userId = me.GetUserId();

        var members = req.MemberUserIds?.Distinct().Where(x => x != Guid.Empty).ToArray() ?? Array.Empty<Guid>();

        Audit.Info("conversation.add_members.attempt", new
        {
            userId,
            conversationId,
            membersCount = members.Length
        });

        var res = await conversations.AddMembersAsync(userId, conversationId, members, ct);
        if (!res.IsSuccess)
        {
            Audit.Warn("conversation.add_members.failed", new
            {
                userId,
                conversationId,
                code = res.Error!.Code
            });
            return res.ToHttp(ctx);
        }

        Audit.Info("conversation.add_members.success", new
        {
            userId,
            conversationId,
            membersCount = members.Length
        });

        return Results.NoContent();
    }

    private static async Task<IResult> RemoveMember(
        HttpContext ctx,
        ClaimsPrincipal me,
        Guid conversationId,
        Guid memberUserId,
        IConversationService conversations,
        CancellationToken ct)
    {
        var userId = me.GetUserId();

        Audit.Info("conversation.remove_member.attempt", new
        {
            userId,
            conversationId,
            memberUserId
        });

        var res = await conversations.RemoveMemberAsync(userId, conversationId, memberUserId, ct);
        if (!res.IsSuccess)
        {
            Audit.Warn("conversation.remove_member.failed", new
            {
                userId,
                conversationId,
                memberUserId,
                code = res.Error!.Code
            });
            return res.ToHttp(ctx);
        }

        Audit.Info("conversation.remove_member.success", new
        {
            userId,
            conversationId,
            memberUserId
        });

        return Results.NoContent();
    }

    private static async Task<IResult> Leave(
        HttpContext ctx,
        ClaimsPrincipal me,
        Guid conversationId,
        IConversationService conversations,
        CancellationToken ct)
    {
        var userId = me.GetUserId();

        Audit.Info("conversation.leave.attempt", new
        {
            userId,
            conversationId
        });

        var res = await conversations.LeaveAsync(userId, conversationId, ct);
        if (!res.IsSuccess)
        {
            Audit.Warn("conversation.leave.failed", new
            {
                userId,
                conversationId,
                code = res.Error!.Code
            });
            return res.ToHttp(ctx);
        }

        Audit.Info("conversation.leave.success", new
        {
            userId,
            conversationId
        });

        return Results.NoContent();
    }
}