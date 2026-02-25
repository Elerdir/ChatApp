using ChatApp.Domain.Users;

namespace ChatApp.Application.Security;

public interface ITokenService
{
    string CreateAccessToken(User user, Guid deviceId);
}