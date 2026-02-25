using ChatApp.Api.Common;

namespace ChatApp.Application.Devices;

public interface IDeviceService
{
    Task<Result<IReadOnlyList<DeviceDto>>> GetMyDevicesAsync(Guid userId, CancellationToken ct = default);
    Task<Result> RevokeDeviceAsync(Guid userId, Guid deviceId, CancellationToken ct = default);
    Task<Result> RenameDeviceAsync(Guid userId, Guid deviceId, RenameDeviceRequest req, CancellationToken ct = default); 
}