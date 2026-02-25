using System.Diagnostics;
using ChatApp.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Common;

public static class ProblemResults
{
    public static IResult ToHttp(this AppError error, HttpContext ctx)
    {
        var status = error.Type switch
        {
            ErrorType.Validation   => StatusCodes.Status400BadRequest,
            ErrorType.Conflict     => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden    => StatusCodes.Status403Forbidden,
            ErrorType.NotFound     => StatusCodes.Status404NotFound,
            ErrorType.RateLimited  => StatusCodes.Status429TooManyRequests,
            _                      => StatusCodes.Status500InternalServerError
        };

        var corrId = GetCorrelationId(ctx);
        var traceId = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;

        if (error.Type == ErrorType.Validation)
        {
            var fieldErrors = error.FieldErrors is null
                ? new Dictionary<string, string[]>()
                : new Dictionary<string, string[]>(error.FieldErrors);

            var vpd = new ValidationProblemDetails(fieldErrors)
            {
                Status = status,
                Title = error.Message,
                Type = $"urn:chatapp:error:{error.Code}"
            };

            vpd.Extensions["code"] = error.Code;
            if (!string.IsNullOrWhiteSpace(corrId)) vpd.Extensions["correlationId"] = corrId;
            if (!string.IsNullOrWhiteSpace(traceId)) vpd.Extensions["traceId"] = traceId;

            return Results.Problem(vpd);
        }

        var pd = new ProblemDetails
        {
            Status = status,
            Title = error.Message,
            Type = $"urn:chatapp:error:{error.Code}"
        };

        pd.Extensions["code"] = error.Code;
        if (!string.IsNullOrWhiteSpace(corrId)) pd.Extensions["correlationId"] = corrId;
        if (!string.IsNullOrWhiteSpace(traceId)) pd.Extensions["traceId"] = traceId;

        

        return Results.Problem(pd);
    }

    public static IResult ToHttp<T>(this Result<T> result, HttpContext ctx)
        => result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToHttp(ctx);

    public static IResult ToHttp(this Result result, HttpContext ctx)
        => result.IsSuccess ? Results.NoContent() : result.Error!.ToHttp(ctx);

    // ---- helpers ----

    private static string? GetCorrelationId(HttpContext ctx)
    {
        // 1) z middleware Items (pokud máš CorrelationIdMiddleware)
        if (ctx.Items.TryGetValue("X-Correlation-Id", out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
            return s;

        // 2) z headeru
        if (ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var h) && !string.IsNullOrWhiteSpace(h))
            return h.ToString();

        return null;
    }
}