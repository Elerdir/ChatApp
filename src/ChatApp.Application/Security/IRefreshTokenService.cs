using System.Security.Cryptography;
using System.Text;

namespace ChatApp.Infrastructure.Auth;

public interface IRefreshTokenService
{
    string GenerateToken();
    string HashToken(string token);
}