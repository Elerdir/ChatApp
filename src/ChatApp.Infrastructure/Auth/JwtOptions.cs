namespace ChatApp.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = "ChatApp";
    public string Audience { get; init; } = "ChatAppClients";
    public string SigningKey { get; init; } = default!; // min 32 chars
    public int AccessTokenMinutes { get; init; } = 60;
}