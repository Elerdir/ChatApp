using System.Security.Claims;
using ChatApp.Api.Common;
using ChatApp.Application.Users;

namespace ChatApp.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me")
            .WithTags("Me")
            .RequireAuthorization()
            .RequireRateLimiting("default"); // volitelné

        group.MapGet("/", GetMe);
        group.MapPatch("/profile", PatchProfile);

        group.MapGet("/settings", GetSettings);
        group.MapPatch("/settings/global", PatchGlobalSettings);
        group.MapPatch("/settings/device", PatchDeviceSettings);

        return app;
    }

    private static async Task<IResult> GetMe(
        HttpContext ctx,
        ClaimsPrincipal me,
        IProfileService profile,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var res = await profile.GetMeAsync(userId, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> PatchProfile(
        HttpContext ctx,
        ClaimsPrincipal me,
        PatchProfileRequest req,
        IProfileService profile,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var res = await profile.UpdateProfileAsync(userId, req, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> GetSettings(
        HttpContext ctx,
        ClaimsPrincipal me,
        ISettingsService settings,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var deviceId = me.GetDeviceId();
        var res = await settings.GetAsync(userId, deviceId, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> PatchGlobalSettings(
        HttpContext ctx,
        ClaimsPrincipal me,
        PatchSettingsRequest req,
        ISettingsService settings,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var res = await settings.PatchGlobalAsync(userId, req, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> PatchDeviceSettings(
        HttpContext ctx,
        ClaimsPrincipal me,
        PatchSettingsRequest req,
        ISettingsService settings,
        CancellationToken ct)
    {
        var deviceId = me.GetDeviceId();
        var res = await settings.PatchDeviceAsync(deviceId, req, ct);
        return res.ToHttp(ctx);
    }
}