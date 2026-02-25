using ChatApp.Api.Common;

namespace ChatApp.Application.Users;

public interface ISettingsService
{
    Task<Result<SettingsDto>> GetAsync(Guid userId, Guid deviceId, CancellationToken ct = default);
    Task<Result<string>> PatchGlobalAsync(Guid userId, PatchSettingsRequest req, CancellationToken ct = default);
    Task<Result<string>> PatchDeviceAsync(Guid deviceId, PatchSettingsRequest req, CancellationToken ct = default);
}