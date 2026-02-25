using System.Security.Claims;
using System.Threading.RateLimiting;
using ChatApp.Api;
using ChatApp.Api.Endpoints;
using ChatApp.Api.Middleware;
using ChatApp.Application;
using ChatApp.Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog (pokud už máš appsettings sekci Serilog, bude to číst odtud)
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services);
});

// DI – tvoje vrstvy (přizpůsob podle toho, jak se jmenují extension metody)
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(); // sem patří tvoje JWT konfigurace
builder.Services.AddAuthorization();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddRateLimiter(o =>
{
    // policy "auth-ip", "send-message", ...
    // (už máš zavedeno)
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// TODO: AddApplicationLayer/AddInfrastructureLayer – podle tvých existujících DI extension metod
// builder.Services.AddApplicationLayer();
// builder.Services.AddInfrastructureLayer();

var app = builder.Build();

// middleware order
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// map endpoints
app.MapAuthEndpoints();
app.MapConversationEndpoints();
app.MapMessageEndpoints();
app.MapUserEndpoints(); // pokud už máš

// health
app.MapHealthChecks("/health");

// migrations (pokud už máš DatabaseInitializer)
// await DatabaseInitializer.MigrateAsync(app.Services);

app.Run();