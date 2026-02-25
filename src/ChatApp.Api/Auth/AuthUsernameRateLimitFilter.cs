using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using ChatApp.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Auth;

public sealed class AuthUsernameRateLimitFilter : IEndpointFilter
{
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _byUser = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        // očekáváme LoginRequest v arguments
        var login = ctx.Arguments.OfType<LoginRequest>().FirstOrDefault();
        if (login is null) return await next(ctx);

        var username = (login.Username ?? "").Trim().ToLowerInvariant();
        if (username.Length == 0) return await next(ctx);

        var limiter = _byUser.GetOrAdd(username, _ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            AutoReplenishment = true,
            QueueLimit = 0
        }));

        using var lease = limiter.AttemptAcquire(1);
        if (!lease.IsAcquired)
        {
            var pd = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many login attempts",
                Type = "urn:chatapp:error:rate_limited"
            };
            pd.Extensions["code"] = "auth.too_many_attempts";

            return Results.Problem(pd);
        }

        return await next(ctx);
    }
}