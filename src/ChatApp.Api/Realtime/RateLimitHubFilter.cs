using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.Api.Realtime;

public sealed class RateLimitHubFilter : IHubFilter
{
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters = new();

    // 120 zpráv / min / user (uprav si)
    private static TokenBucketRateLimiter CreateLimiter() =>
        new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 120,
            TokensPerPeriod = 120,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            AutoReplenishment = true,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        // limituj jen vybrané metody
        if (!string.Equals(invocationContext.HubMethodName, "SendMessage", StringComparison.Ordinal))
            return await next(invocationContext);

        var userId = invocationContext.Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Unauthorized.");

        var limiter = _limiters.GetOrAdd(userId, _ => CreateLimiter());
        using var lease = limiter.AttemptAcquire(1);

        if (!lease.IsAcquired)
            throw new HubException("Rate limit exceeded.");

        return await next(invocationContext);
    }
}