

using System.Text.Json;
using ChatApp.Api.Common;
using ChatApp.Application.Abstractions;
using ChatApp.Application.Common;
using ChatApp.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Application.Users;

public sealed class UserService : IUserService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _db;

    public UserService(IAppDbContext db) => _db = db;

    public async Task<Result<UserMeDto>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Username })
            .SingleOrDefaultAsync(ct);

        if (user is null)
        {
            return Result<UserMeDto>.Fail(new AppError(
                Code: "users.not_found",
                Message: "User not found.",
                Type: ErrorType.NotFound));
        }

        var profile = await _db.UserProfiles
            .Where(p => p.UserId == userId)
            .Select(p => new { p.DisplayName, p.AvatarUrl })
            .SingleOrDefaultAsync(ct);

        return Result<UserMeDto>.Ok(new UserMeDto(
            UserId: user.Id,
            Username: user.Username,
            DisplayName: profile?.DisplayName,
            AvatarUrl: profile?.AvatarUrl
        ));
    }

    public async Task<Result> UpdateProfileAsync(Guid userId, UpdateProfileRequest req, CancellationToken ct = default)
    {
        var displayName = (req.DisplayName ?? "").Trim();
        if (displayName.Length is < 1 or > 80)
        {
            return Result.Fail(new AppError(
                Code: "validation.failed",
                Message: "Validation failed.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["displayName"] = new[] { "DisplayName must be 1-80 characters." }
                }));
        }

        // zajisti, že user existuje
        var exists = await _db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!exists)
        {
            return Result.Fail(new AppError(
                Code: "users.not_found",
                Message: "User not found.",
                Type: ErrorType.NotFound));
        }

        var profile = await _db.UserProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            // pokud profil ještě není, založ
            profile = new UserProfile
            {
                UserId = userId,
                DisplayName = displayName
            };
            _db.UserProfiles.Add(profile);
        }
        else
        {
            profile.DisplayName = displayName;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> UpdateSettingsAsync(
        Guid userId,
        Guid deviceId,
        UpdateUserSettingsRequest req,
        CancellationToken ct = default)
    {
        // Scope: "global" nebo "device"
        var scope = (req.Scope ?? "").Trim().ToLowerInvariant();
        if (scope is not ("global" or "device"))
        {
            return Result.Fail(new AppError(
                Code: "validation.failed",
                Message: "Validation failed.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["scope"] = new[] { "Scope must be 'global' or 'device'." }
                }));
        }

        if (req.Settings is null)
        {
            return Result.Fail(new AppError(
                Code: "validation.failed",
                Message: "Validation failed.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["settings"] = new[] { "Settings is required." }
                }));
        }

        // validace key/value základ
        foreach (var kv in req.Settings)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Key.Length > 64)
            {
                return Result.Fail(new AppError(
                    Code: "validation.failed",
                    Message: "Validation failed.",
                    Type: ErrorType.Validation,
                    FieldErrors: new Dictionary<string, string[]>
                    {
                        ["settings"] = new[] { "Setting keys must be 1-64 characters." }
                    }));
            }
        }

        // ensure user exists
        var exists = await _db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!exists)
        {
            return Result.Fail(new AppError(
                Code: "users.not_found",
                Message: "User not found.",
                Type: ErrorType.NotFound));
        }

        var json = JsonSerializer.Serialize(req.Settings, JsonOptions);

        if (scope == "global")
        {
            var entity = await _db.UserSettingsGlobals
                .SingleOrDefaultAsync(x => x.UserId == userId, ct);

            if (entity is null)
            {
                entity = new UserSettingsGlobal
                {
                    UserId = userId,
                    SettingsJson = json,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _db.UserSettingsGlobals.Add(entity);
            }
            else
            {
                entity.SettingsJson = json;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        else // device
        {
            if (deviceId == Guid.Empty)
            {
                return Result.Fail(new AppError(
                    Code: "devices.device_required",
                    Message: "DeviceId is required for device-scoped settings.",
                    Type: ErrorType.Validation));
            }

            var entity = await _db.UserSettingsDevices
                .SingleOrDefaultAsync(x => x.UserId == userId && x.DeviceId == deviceId, ct);

            if (entity is null)
            {
                entity = new UserSettingsDevice
                {
                    UserId = userId,
                    DeviceId = deviceId,
                    SettingsJson = json,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _db.UserSettingsDevices.Add(entity);
            }
            else
            {
                entity.SettingsJson = json;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}