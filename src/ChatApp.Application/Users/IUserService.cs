using ChatApp.Api.Common;

namespace ChatApp.Application.Users;

public interface IUserService
{
    Task<Result<UserMeDto>> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task<Result> UpdateProfileAsync(Guid userId, UpdateProfileRequest req, CancellationToken ct = default);
    Task<Result> UpdateSettingsAsync(Guid userId, Guid deviceId, UpdateUserSettingsRequest req, CancellationToken ct = default);
}