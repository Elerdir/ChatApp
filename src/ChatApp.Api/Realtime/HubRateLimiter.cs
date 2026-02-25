using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace ChatApp.Api.Realtime;

public sealed class HubRateLimiter
{
    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters = new();

    public RateLimitLease TryAcquire(string key, int permits = 1)
    {
        var limiter = _limiters.GetOrAdd(key, _ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 20,                 // max burst
            TokensPerPeriod = 20,            // refill
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
            AutoReplenishment = true,
            QueueLimit = 0
        }));

        return limiter.AttemptAcquire(permits);
    }
}