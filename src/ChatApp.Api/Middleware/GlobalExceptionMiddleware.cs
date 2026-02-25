using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // klient zavřel spojení
            ctx.Response.StatusCode = 499; // optional
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ctx, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext ctx, Exception ex)
    {
        _logger.LogError(ex, "Unhandled exception");

        if (ctx.Response.HasStarted)
            throw ex;

        var correlationId = GetCorrelationId(ctx);
        var traceId = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;

        var pd = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Unhandled server error",
            Type = "urn:chatapp:error:server.unhandled",
            Detail = "An unexpected error occurred."
        };

        pd.Extensions["code"] = "server.unhandled";
        if (!string.IsNullOrWhiteSpace(correlationId))
            pd.Extensions["correlationId"] = correlationId;
        if (!string.IsNullOrWhiteSpace(traceId))
            pd.Extensions["traceId"] = traceId;

        ctx.Response.StatusCode = pd.Status.Value;
        ctx.Response.ContentType = "application/problem+json";

        await ctx.Response.WriteAsJsonAsync(pd);
    }

    private static string? GetCorrelationId(HttpContext ctx)
    {
        if (ctx.Items.TryGetValue("X-Correlation-Id", out var v) && v is string s)
            return s;

        if (ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var h))
            return h.ToString();

        return null;
    }
}