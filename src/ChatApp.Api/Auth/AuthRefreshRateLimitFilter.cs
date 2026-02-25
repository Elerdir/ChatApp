using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using ChatApp.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Auth;

public sealed class AuthRefreshRateLimitFilter : IEndpointFilter
{
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _byKey = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var req = ctx.Arguments.OfType<RefreshRequest>().FirstOrDefault();
        if (req is null) return await next(ctx);

        // předpoklad: RefreshRequest má RefreshToken (string)
        var refreshToken = (req.RefreshToken ?? "").Trim();
        if (refreshToken.Length == 0) return await next(ctx);

        var ip = ctx.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // klíč = ip + hash(token) (token sám nikde neukládáme)
        var key = $"refresh:{ip}:{Sha256Hex(refreshToken)}";

        var limiter = _byKey.GetOrAdd(key, _ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 30,
            TokensPerPeriod = 30,
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
                Title = "Too many refresh attempts",
                Type = "urn:chatapp:error:auth.too_many_refresh"
            };
            pd.Extensions["code"] = "auth.too_many_refresh";
            return Results.Problem(pd);
        }

        return await next(ctx);
    }

    private static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash); // .NET 6+
    }
}