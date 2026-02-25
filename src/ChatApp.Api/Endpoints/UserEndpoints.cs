using System.Security.Claims;
using ChatApp.Api.Common;
using ChatApp.Api.Filters;
using ChatApp.Api.Logging;
using ChatApp.Application.Users;

namespace ChatApp.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/me", GetMe);

        group.MapPut("/me/profile", UpdateProfile)
            .AddEndpointFilter<ValidateBodyFilter<UpdateProfileRequest>>();

        group.MapPut("/me/settings", UpdateSettings)
            .AddEndpointFilter<ValidateBodyFilter<UpdateUserSettingsRequest>>();

        // placeholder – až budeme dělat attachments/storage
        group.MapPost("/me/avatar", UpdateAvatarPlaceholder);

        return app;
    }

    private static async Task<IResult> GetMe(
        HttpContext ctx,
        ClaimsPrincipal me,
        IUserService users,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var res = await users.GetMeAsync(userId, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> UpdateProfile(
        HttpContext ctx,
        ClaimsPrincipal me,
        UpdateProfileRequest req,
        IUserService users,
        CancellationToken ct)
    {
        var userId = me.GetUserId();

        Audit.Info("user.update_profile.attempt", new
        {
            userId,
            displayName = req.DisplayName
        });

        var res = await users.UpdateProfileAsync(userId, req, ct);

        if (!res.IsSuccess)
        {
            Audit.Warn("user.update_profile.failed", new { userId, code = res.Error!.Code });
            return res.ToHttp(ctx);
        }

        Audit.Info("user.update_profile.success", new { userId });
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateSettings(
        HttpContext ctx,
        ClaimsPrincipal me,
        UpdateUserSettingsRequest req,
        IUserService users,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var deviceId = me.GetDeviceId();

        Audit.Info("user.update_settings.attempt", new
        {
            userId,
            deviceId,
            scope = req.Scope
        });

        var res = await users.UpdateSettingsAsync(userId, deviceId, req, ct);

        if (!res.IsSuccess)
        {
            Audit.Warn("user.update_settings.failed", new { userId, deviceId, code = res.Error!.Code });
            return res.ToHttp(ctx);
        }

        Audit.Info("user.update_settings.success", new { userId, deviceId, scope = req.Scope });
        return Results.NoContent();
    }

    private static IResult UpdateAvatarPlaceholder(
        HttpContext ctx)
    {
        // Až budeme dělat attachments/file storage.
        // Teď jen vrátíme "not implemented" v jednotném formátu.
        return new ChatApp.Application.Common.AppError(
            Code: "avatar.not_implemented",
            Message: "Avatar upload will be added with file storage support.",
            Type: ChatApp.Application.Common.ErrorType.Conflict
        ).ToHttp(ctx);
    }
}