namespace ChatApp.Application.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public int RefreshTokenDays { get; init; } = 60;
}