using System.Security.Claims;
using ChatApp.Api.Common;
using ChatApp.Application.Devices;

namespace ChatApp.Api.Endpoints;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me/devices")
            .WithTags("Devices")
            .RequireAuthorization()
            .RequireRateLimiting("default"); // volitelné

        group.MapGet("/", ListMine);

        group.MapPatch("/{deviceId:guid}", Rename);

        group.MapDelete("/{deviceId:guid}", RevokeDevice);

        return app;
    }

    private static async Task<IResult> ListMine(
        HttpContext ctx,
        ClaimsPrincipal me,
        IDeviceService svc,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var res = await svc.GetMyDevicesAsync(userId, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> Rename(
        HttpContext ctx,
        ClaimsPrincipal me,
        Guid deviceId,
        RenameDeviceRequest req,
        IDeviceService svc,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var res = await svc.RenameDeviceAsync(userId, deviceId, req, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> RevokeDevice(
        HttpContext ctx,
        ClaimsPrincipal me,
        Guid deviceId,
        IDeviceService svc,
        CancellationToken ct)
    {
        var userId = me.GetUserId();
        var res = await svc.RevokeDeviceAsync(userId, deviceId, ct);
        return res.ToHttp(ctx);
    }
}