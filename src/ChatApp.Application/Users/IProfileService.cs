using ChatApp.Api.Common;

namespace ChatApp.Application.Users;

public interface IProfileService
{
    Task<Result<MeDto>> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task<Result> UpdateProfileAsync(Guid userId, PatchProfileRequest req, CancellationToken ct = default);
}