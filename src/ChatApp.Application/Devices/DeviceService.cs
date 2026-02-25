using ChatApp.Api.Common;
using ChatApp.Application.Abstractions;
using ChatApp.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Application.Devices;

public sealed class DeviceService : IDeviceService
{
    private readonly IAppDbContext _db;

    public DeviceService(IAppDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<DeviceDto>>> GetMyDevicesAsync(Guid userId, CancellationToken ct = default)
    {
        var devices = await _db.Devices
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new DeviceDto(
                d.Id,
                d.InstallationId,
                d.DeviceName,
                d.Platform,
                d.Os,
                d.AppVersion,
                d.CreatedAt,
                d.LastSeenAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<DeviceDto>>.Ok(devices);
    }

    public async Task<Result> RenameDeviceAsync(Guid userId, Guid deviceId, RenameDeviceRequest req, CancellationToken ct = default)
    {
        if (deviceId == Guid.Empty)
            return Result.Fail(new AppError("devices.invalid_id", "Invalid device id.", ErrorType.Validation));

        if (req.DeviceName is { Length: > 64 })
        {
            return Result.Fail(new AppError(
                Code: "devices.invalid_name",
                Message: "Invalid device name.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["deviceName"] = new[] { "DeviceName max length is 64." }
                }));
        }

        var device = await _db.Devices.SingleOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId, ct);
        if (device is null)
            return Result.Fail(new AppError("devices.not_found", "Device not found.", ErrorType.NotFound));

        device.Rename(string.IsNullOrWhiteSpace(req.DeviceName) ? null : req.DeviceName.Trim());

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RevokeDeviceAsync(Guid userId, Guid deviceId, CancellationToken ct = default)
    {
        if (deviceId == Guid.Empty)
            return Result.Fail(new AppError("devices.invalid_id", "Invalid device id.", ErrorType.Validation));

        var device = await _db.Devices.SingleOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId, ct);
        if (device is null)
            return Result.Fail(new AppError("devices.not_found", "Device not found.", ErrorType.NotFound));

        // revoke all active refresh tokens for this device
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.DeviceId == deviceId && t.RevokedAt == null && t.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        foreach (var t in tokens) t.Revoke();

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}