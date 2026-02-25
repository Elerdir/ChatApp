using System.Security.Claims;
using ChatApp.Api.Auth;
using ChatApp.Api.Common;
using ChatApp.Api.Filters;
using ChatApp.Api.Logging;
using ChatApp.Application.Auth;

namespace ChatApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth");

        group.MapPost("/register", Register)
            .RequireRateLimiting("auth-ip")
            .AddEndpointFilter<ValidateBodyFilter<RegisterRequest>>();

        group.MapPost("/login", Login)
            .RequireRateLimiting("auth-ip")
            .AddEndpointFilter<AuthUsernameRateLimitFilter>()
            .AddEndpointFilter<ValidateBodyFilter<LoginRequest>>();

        group.MapPost("/refresh", Refresh)
            .RequireRateLimiting("auth-ip")
            .AddEndpointFilter<AuthRefreshRateLimitFilter>()
            .AddEndpointFilter<ValidateBodyFilter<RefreshRequest>>();

        group.MapPost("/logout", Logout)
            .RequireAuthorization()
            .RequireRateLimiting("auth-ip")
            .AddEndpointFilter<ValidateBodyFilter<LogoutRequest>>();

        group.MapPost("/logout-all", LogoutAll)
            .RequireAuthorization()
            .RequireRateLimiting("auth-ip");

        return app;
    }

    private static async Task<IResult> Register(
        HttpContext ctx,
        RegisterRequest req,
        IAuthService auth,
        CancellationToken ct)
    {
        var res = await auth.RegisterAsync(req, ct);
        return res.ToHttp(ctx);
    }

    private static async Task<IResult> Login(
        HttpContext ctx,
        LoginRequest req,
        IAuthService auth,
        CancellationToken ct)
    {
        // audit: pokus (neukládej password!)
        Audit.Info("auth.login.attempt", new
        {
            username = req.Username,
            ip = ctx.Connection.RemoteIpAddress?.ToString()
        });

        var res = await auth.LoginAsync(req, ct);

        if (res.IsSuccess)
        {
            Audit.Info("auth.login.success", new
            {
                username = req.Username,
                ip = ctx.Connection.RemoteIpAddress?.ToString()
            });
            return res.ToHttp(ctx);
        }

        // neprozrazuj detail typu "user not found" vs "bad password"
        Audit.Warn("auth.login.failed", new
        {
            username = req.Username,
            ip = ctx.Connection.RemoteIpAddress?.ToString(),
            code = res.Error!.Code
        });

        return res.ToHttp(ctx);
    }

    private static async Task<IResult> Refresh(
        HttpContext ctx,
        RefreshRequest req,
        IAuthService auth,
        CancellationToken ct)
    {
        Audit.Info("auth.refresh.attempt", new { ip = ctx.Connection.RemoteIpAddress?.ToString() });

        var res = await auth.RefreshAsync(req, ct);

        Audit.Info(res.IsSuccess ? "auth.refresh.success" : "auth.refresh.failed",
            new { ip = ctx.Connection.RemoteIpAddress?.ToString(), code = res.IsSuccess ? null : res.Error!.Code });

        return res.ToHttp(ctx);
    }

    private static async Task<IResult> Logout(
        HttpContext ctx,
        LogoutRequest req,
        IAuthService auth,
        CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();

        Audit.Info("auth.logout.attempt", new { userId });

        var res = await auth.LogoutAsync(req, ct);

        Audit.Info(res.IsSuccess ? "auth.logout.success" : "auth.logout.failed",
            new { userId, code = res.IsSuccess ? null : res.Error!.Code });

        return res.ToHttp(ctx);
    }

    private static async Task<IResult> LogoutAll(
        HttpContext ctx,
        ClaimsPrincipal me,
        IAuthService auth,
        CancellationToken ct)
    {
        var userId = me.GetUserId();

        Audit.Info("auth.logout_all.attempt", new { userId });

        var res = await auth.LogoutAllAsync(userId, ct);

        Audit.Info(res.IsSuccess ? "auth.logout_all.success" : "auth.logout_all.failed",
            new { userId, code = res.IsSuccess ? null : res.Error!.Code });

        return res.ToHttp(ctx);
    }
}