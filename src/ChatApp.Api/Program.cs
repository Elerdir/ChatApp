using System.Security.Claims;
using System.Threading.RateLimiting;
using ChatApp.Api;
using ChatApp.Api.Middleware;
using ChatApp.Application;
using ChatApp.Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddApiServices();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = HttpLoggingFields.All;
    o.RequestBodyLogLimit = 4096;
    o.ResponseBodyLogLimit = 4096;
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Default")!,
        name: "postgres",
        timeout: TimeSpan.FromSeconds(5));

builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (ctx, ct) =>
    {
        var http = ctx.HttpContext;

        var pd = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Type = "urn:chatapp:error:rate_limited"
        };

        pd.Extensions["code"] = "rate_limited";
        if (http.Items.TryGetValue("X-Correlation-Id", out var v) && v is string s)
            pd.Extensions["correlationId"] = s;

        http.Response.ContentType = "application/problem+json";
        await http.Response.WriteAsJsonAsync(pd, ct);
    };

    // 1) Per-user (globální) – např. 120 requestů / 60s
    options.AddPolicy("per-user", http =>
    {
        var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? http.User.FindFirst("sub")?.Value
                     ?? http.Connection.RemoteIpAddress?.ToString()
                     ?? "anon";

        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromSeconds(60),
            QueueLimit = 0
        });
    });

    // 2) Per-user + per-conversation (anti-spam na send message)
    //    např. 20 zpráv / 10s pro jednu konverzaci od jednoho usera
    options.AddPolicy("send-message", http =>
    {
        var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? http.User.FindFirst("sub")?.Value
                     ?? "anon";

        // conversationId je route param v /conversations/{conversationId}/messages
        var conversationId = http.Request.RouteValues.TryGetValue("conversationId", out var cid)
            ? cid?.ToString()
            : "none";

        var key = $"{userId}:{conversationId}";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0
        });
    });
    
    options.AddPolicy("auth-ip", http =>
    {
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"auth:{ip}";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

builder.Services.AddSingleton<ChatApp.Api.Realtime.HubRateLimiter>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<LogContextMiddleware>();  

app.UseSerilogRequestLogging(o =>
{
    o.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("host", http.Request.Host.Value);
        diag.Set("query", http.Request.QueryString.Value ?? "");
        diag.Set("statusCode", http.Response.StatusCode);
    };
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpLogging();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.UseHttpLogging();
app.UseApiPipeline();

app.MapApiEndpoints();
app.MapRealtime();

app.MapHealthChecks("/health");

await ChatApp.Infrastructure.Persistence.DatabaseInitializer
    .MigrateAsync(app.Services);

app.Run();