namespace ChatApp.Application.Security;

public interface IRefreshTokenService
{
    string GenerateToken();
    string HashToken(string token);
}