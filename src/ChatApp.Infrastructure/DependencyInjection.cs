using System.Text;
using ChatApp.Application.Abstractions;
using ChatApp.Application.Auth;
using ChatApp.Application.Security;
using ChatApp.Infrastructure.Auth;
using ChatApp.Infrastructure.Persistence;
using ChatApp.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ChatApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<JwtOptions>(cfg.GetSection(JwtOptions.SectionName));

        services.AddDbContext<AppDbContext>(opt =>
        {
            opt.UseNpgsql(cfg.GetConnectionString("Postgres"));
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        
        services.Configure<AuthOptions>(cfg.GetSection(AuthOptions.SectionName));
        services.AddSingleton<IClock, SystemClock>();

        // Auth
        var jwt = cfg.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(10)
                };

                // SignalR: umožni token přes query string ?access_token=
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

        return services;
    }
}