using System.Text.Json;
using ChatApp.Api.Common;
using ChatApp.Application.Abstractions;
using ChatApp.Application.Common;
using ChatApp.Domain.Users;

namespace ChatApp.Application.Users;

public sealed class SettingsService : ISettingsService
{
    private readonly IAppDbContext _db;

    public SettingsService(IAppDbContext db) => _db = db;

    public async Task<Result<SettingsDto>> GetAsync(Guid userId, Guid deviceId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || deviceId == Guid.Empty)
            return Result<SettingsDto>.Fail(new AppError("settings.invalid_input", "Invalid input.", ErrorType.Validation));

        var global = await _db.UserSettings.FindAsync([userId], ct);
        if (global is null)
        {
            global = new UserSettings(userId);
            _db.UserSettings.Add(global);
        }

        var device = await _db.DeviceSettings.FindAsync([deviceId], ct);
        if (device is null)
        {
            device = new DeviceSettings(deviceId);
            _db.DeviceSettings.Add(device);
        }

        await _db.SaveChangesAsync(ct);

        // effective = merge(global, device)
        var devicePatch = JsonDocument.Parse(device.SettingsJson).RootElement;
        var effective = JsonMerge.Merge(global.SettingsJson, devicePatch);

        return Result<SettingsDto>.Ok(new SettingsDto(global.SettingsJson, device.SettingsJson, effective));
    }

    public async Task<Result<string>> PatchGlobalAsync(Guid userId, PatchSettingsRequest req, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return Result<string>.Fail(new AppError("settings.invalid_input", "Invalid input.", ErrorType.Validation));

        if (req.Patch.ValueKind != JsonValueKind.Object)
        {
            return Result<string>.Fail(new AppError(
                Code: "settings.invalid_patch",
                Message: "Patch must be a JSON object.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["patch"] = new[] { "Expected JSON object." }
                }));
        }

        var row = await _db.UserSettings.FindAsync([userId], ct);
        if (row is null)
        {
            row = new UserSettings(userId);
            _db.UserSettings.Add(row);
        }

        row.Set(JsonMerge.Merge(row.SettingsJson, req.Patch));
        await _db.SaveChangesAsync(ct);

        return Result<string>.Ok(row.SettingsJson);
    }

    public async Task<Result<string>> PatchDeviceAsync(Guid deviceId, PatchSettingsRequest req, CancellationToken ct = default)
    {
        if (deviceId == Guid.Empty)
            return Result<string>.Fail(new AppError("settings.invalid_input", "Invalid input.", ErrorType.Validation));

        if (req.Patch.ValueKind != JsonValueKind.Object)
        {
            return Result<string>.Fail(new AppError(
                Code: "settings.invalid_patch",
                Message: "Patch must be a JSON object.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["patch"] = new[] { "Expected JSON object." }
                }));
        }

        var row = await _db.DeviceSettings.FindAsync([deviceId], ct);
        if (row is null)
        {
            row = new DeviceSettings(deviceId);
            _db.DeviceSettings.Add(row);
        }

        row.Set(JsonMerge.Merge(row.SettingsJson, req.Patch));
        await _db.SaveChangesAsync(ct);

        return Result<string>.Ok(row.SettingsJson);
    }
}