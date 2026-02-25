using System.Diagnostics;

namespace ChatApp.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        var corrId = ctx.Request.Headers.TryGetValue(HeaderName, out var incoming) && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString("N");

        ctx.Items[HeaderName] = corrId;
        ctx.Response.Headers[HeaderName] = corrId;

        using (_logger.BeginScope(new Dictionary<string, object?>
               {
                   ["correlation_id"] = corrId,
                   ["trace_id"] = Activity.Current?.TraceId.ToString()
               }))
        {
            await _next(ctx);
        }
    }
}