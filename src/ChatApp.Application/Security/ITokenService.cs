using ChatApp.Domain.Users;

namespace ChatApp.Infrastructure.Auth;

public interface ITokenService
{
    string CreateAccessToken(User user, Guid deviceId);
}