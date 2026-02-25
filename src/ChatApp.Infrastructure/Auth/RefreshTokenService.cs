using System.Security.Cryptography;
using System.Text;
using ChatApp.Application.Security;

namespace ChatApp.Infrastructure.Auth;

public sealed class RefreshTokenService : IRefreshTokenService
{
    // jednoduchý "pepper" – dej do configu (např. RefreshTokens:Pepper)
    private readonly string _pepper;

    public RefreshTokenService(Microsoft.Extensions.Configuration.IConfiguration cfg)
    {
        _pepper = cfg["RefreshTokens:Pepper"] ?? throw new InvalidOperationException("Missing RefreshTokens:Pepper");
    }

    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    public string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token + _pepper);
        return Convert.ToHexString(sha.ComputeHash(bytes)); // uppercase hex
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}