using System.Security.Claims;
using System.Text;
using ChatApp.Api.Endpoints;
using ChatApp.Api.Realtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace ChatApp.Api;

public static class ApiBootstrap
{
    public static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // JWT
        var jwt = config.GetSection("Jwt");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = jwt["Issuer"],
                    ValidAudience = jwt["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt["Key"]!)),

                    NameClaimType = ClaimTypes.NameIdentifier
                };

                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(token) &&
                            path.StartsWithSegments("/hubs/chat"))
                            ctx.Token = token;

                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization();

        builder.Services.AddSignalR();

        builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

        builder.Services.AddRateLimiter(opt =>
        {
            opt.AddFixedWindowLimiter("default", o =>
            {
                o.PermitLimit = 100;
                o.Window = TimeSpan.FromSeconds(10);
            });
        });

        return builder;
    }

    public static void UseApiPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
    }

    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapAuthEndpoints();
        app.MapConversationEndpoints();
        app.MapMessageEndpoints();
        app.MapUserEndpoints();
        app.MapDeviceEndpoints();
        app.MapMeEndpoints();
    }

    public static void MapRealtime(this WebApplication app)
    {
        app.MapHub<ChatHub>("/hubs/chat");
    }
}