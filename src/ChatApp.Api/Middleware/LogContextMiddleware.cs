using System.Diagnostics;
using Serilog.Context;

namespace ChatApp.Api.Middleware;

public sealed class LogContextMiddleware
{
    private readonly RequestDelegate _next;

    public LogContextMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var corrId = GetCorrelationId(ctx);
        var traceId = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;

        var userId = ctx.User?.FindFirst("sub")?.Value
                     ?? ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var deviceId = ctx.User?.FindFirst("deviceId")?.Value;

        using (LogContext.PushProperty("correlationId", corrId ?? ""))
        using (LogContext.PushProperty("traceId", traceId ?? ""))
        using (LogContext.PushProperty("userId", userId ?? ""))
        using (LogContext.PushProperty("deviceId", deviceId ?? ""))
        using (LogContext.PushProperty("path", ctx.Request.Path.Value ?? ""))
        using (LogContext.PushProperty("method", ctx.Request.Method))
        {
            await _next(ctx);
        }
    }

    private static string? GetCorrelationId(HttpContext ctx)
    {
        // pokud máš CorrelationIdMiddleware, tak tohle sedí
        if (ctx.Items.TryGetValue("X-Correlation-Id", out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
            return s;

        if (ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var h) && !string.IsNullOrWhiteSpace(h))
            return h.ToString();

        return null;
    }
}