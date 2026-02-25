using ChatApp.Api.Endpoints;
using ChatApp.Api.Middleware;
using ChatApp.Api.Realtime;
using ChatApp.Application;
using ChatApp.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog (pokud máš Serilog v appsettings.json)
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services);
});

// Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Minimal API extras
builder.Services.AddHttpContextAccessor();

// FluentValidation (ValidateBodyFilter<T>)
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rate limiting (minimální – přidej si další policies dle potřeby)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth-ip", http =>
    {
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"auth:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("send-message", http =>
    {
        var userId =
            http.User.FindFirst("sub")?.Value ??
            http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
            "anon";

        var conversationId = http.Request.RouteValues.TryGetValue("conversationId", out var cid)
            ? cid?.ToString()
            : "none";

        return RateLimitPartition.GetFixedWindowLimiter($"{userId}:{conversationId}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0
        });
    });
});

// SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<HubRateLimiter>(); // anti-spam pro Hub

var app = builder.Build();

// Middleware order
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// Map endpoints
app.MapAuthEndpoints();
app.MapConversationEndpoints();
app.MapMessageEndpoints();

// SignalR hub
app.MapHub<ChatHub>("/hubs/chat");

app.Run();