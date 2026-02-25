using ChatApp.Api.Common;
using ChatApp.Application.Abstractions;
using ChatApp.Application.Common;
using ChatApp.Application.Security;
using ChatApp.Domain.Auth;
using ChatApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChatApp.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenService _refresh;
    private readonly IPasswordHasher _passwords;
    private readonly IClock _clock;
    private readonly AuthOptions _opt;

    public AuthService(
        IAppDbContext db,
        ITokenService tokens,
        IRefreshTokenService refresh,
        IPasswordHasher passwords,
        IClock clock,
        IOptions<AuthOptions> opt)
    {
        _db = db;
        _tokens = tokens;
        _refresh = refresh;
        _passwords = passwords;
        _clock = clock;
        _opt = opt.Value;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        var username = NormalizeUsername(req.Username);

        var fieldErrors = ValidateRegister(req, username);
        if (fieldErrors.Count > 0)
        {
            return Result<AuthResponse>.Fail(new AppError(
                Code: "auth.invalid_input",
                Message: "Invalid input.",
                Type: ErrorType.Validation,
                FieldErrors: fieldErrors));
        }

        var exists = await _db.Users.AnyAsync(u => u.Username == username, ct);
        if (exists)
        {
            return Result<AuthResponse>.Fail(new AppError(
                Code: "auth.username_taken",
                Message: "Username already taken.",
                Type: ErrorType.Conflict));
        }

        var user = new User(username, req.DisplayName, _passwords.Hash(req.Password));
        _db.Users.Add(user);

        // profile + global settings
        _db.UserProfiles.Add(new UserProfile(user.Id, req.DisplayName));
        _db.UserSettings.Add(new UserSettings(user.Id)); // "{}"

        // device (+ ensure device settings)
        var device = await UpsertDeviceAsync(user.Id, req.Device, ct);
        await EnsureDeviceSettingsAsync(device.Id, ct);

        // tokens
        var (access, refreshPlain) = IssueTokens(user, device.Id);

        var refreshHash = _refresh.HashToken(refreshPlain);
        _db.RefreshTokens.Add(new RefreshToken(
            userId: user.Id,
            deviceId: device.Id,
            tokenHash: refreshHash,
            expiresAt: _clock.UtcNow.AddDays(_opt.RefreshTokenDays)));

        await _db.SaveChangesAsync(ct);

        return Result<AuthResponse>.Ok(new AuthResponse(
            AccessToken: access,
            RefreshToken: refreshPlain,
            UserId: user.Id,
            Username: user.Username,
            DisplayName: req.DisplayName,
            DeviceId: device.Id));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var username = NormalizeUsername(req.Username);

        var fieldErrors = ValidateLogin(req, username);
        if (fieldErrors.Count > 0)
        {
            return Result<AuthResponse>.Fail(new AppError(
                Code: "auth.invalid_input",
                Message: "Invalid input.",
                Type: ErrorType.Validation,
                FieldErrors: fieldErrors));
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == username, ct);
        if (user is null)
        {
            return Result<AuthResponse>.Fail(new AppError(
                Code: "auth.invalid_credentials",
                Message: "Invalid credentials.",
                Type: ErrorType.Unauthorized));
        }

        if (!_passwords.Verify(req.Password, user.PasswordHash))
        {
            return Result<AuthResponse>.Fail(new AppError(
                Code: "auth.invalid_credentials",
                Message: "Invalid credentials.",
                Type: ErrorType.Unauthorized));
        }

        var device = await UpsertDeviceAsync(user.Id, req.Device, ct);
        await EnsureDeviceSettingsAsync(device.Id, ct);

        var profile = await _db.UserProfiles.FindAsync([user.Id], ct);
        var displayName = profile?.DisplayName ?? user.Username;

        var (access, refreshPlain) = IssueTokens(user, device.Id);

        var refreshHash = _refresh.HashToken(refreshPlain);
        _db.RefreshTokens.Add(new RefreshToken(
            userId: user.Id,
            deviceId: device.Id,
            tokenHash: refreshHash,
            expiresAt: _clock.UtcNow.AddDays(_opt.RefreshTokenDays)));

        await _db.SaveChangesAsync(ct);

        return Result<AuthResponse>.Ok(new AuthResponse(
            AccessToken: access,
            RefreshToken: refreshPlain,
            UserId: user.Id,
            Username: user.Username,
            DisplayName: displayName,
            DeviceId: device.Id));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
        {
            return Result<AuthResponse>.Fail(new AppError(
                Code: "auth.refresh_invalid",
                Message: "Refresh token is missing.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["refreshToken"] = new[] { "Refresh token is required." }
                }));
        }

        var hash = _refresh.HashToken(req.RefreshToken);

        var tokenRow = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (tokenRow is null || !tokenRow.IsActive)
        {
            return Result<AuthResponse>.Fail(new AppError(
                Code: "auth.refresh_invalid",
                Message: "Refresh token is invalid.",
                Type: ErrorType.Unauthorized));
        }

        var user = await _db.Users.FindAsync([tokenRow.UserId], ct);
        if (user is null)
        {
            return Result<AuthResponse>.Fail(new AppError(
                Code: "auth.refresh_invalid",
                Message: "Refresh token is invalid.",
                Type: ErrorType.Unauthorized));
        }

        // Rotate refresh token
        var newPlain = _refresh.GenerateToken();
        var newHash = _refresh.HashToken(newPlain);

        var newRt = new RefreshToken(
            userId: user.Id,
            deviceId: tokenRow.DeviceId,
            tokenHash: newHash,
            expiresAt: _clock.UtcNow.AddDays(_opt.RefreshTokenDays));

        _db.RefreshTokens.Add(newRt);
        tokenRow.Revoke(newRt.Id);

        // touch device
        var device = await _db.Devices.FindAsync([tokenRow.DeviceId], ct);
        device?.Touch(appVersion: null);

        var profile = await _db.UserProfiles.FindAsync([user.Id], ct);
        var displayName = profile?.DisplayName ?? user.Username;

        var access = _tokens.CreateAccessToken(user, tokenRow.DeviceId);

        await _db.SaveChangesAsync(ct);

        return Result<AuthResponse>.Ok(new AuthResponse(
            AccessToken: access,
            RefreshToken: newPlain,
            UserId: user.Id,
            Username: user.Username,
            DisplayName: displayName,
            DeviceId: tokenRow.DeviceId));
    }

    public async Task<Result> LogoutAsync(LogoutRequest req, CancellationToken ct = default)
    {
        // idempotent
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return Result.Ok();

        var hash = _refresh.HashToken(req.RefreshToken);
        var tokenRow = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (tokenRow is null)
            return Result.Ok();

        tokenRow.Revoke();
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> LogoutAllAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var t in active) t.Revoke();

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ----------------- helpers -----------------

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    private static Dictionary<string, string[]> ValidateRegister(RegisterRequest req, string normalizedUsername)
    {
        var errors = new Dictionary<string, string[]>();

        if (normalizedUsername.Length < 3 || normalizedUsername.Length > 64)
            errors["username"] = new[] { "Username must be 3-64 characters." };

        var dn = req.DisplayName?.Trim() ?? "";
        if (dn.Length < 1 || dn.Length > 128)
            errors["displayName"] = new[] { "Display name must be 1-128 characters." };

        if (req.Password is null || req.Password.Length < 8)
            errors["password"] = new[] { "Password must be at least 8 characters." };

        if (req.Device is null)
            errors["device"] = new[] { "Device info is required." };
        else
        {
            if (req.Device.InstallationId == Guid.Empty)
                errors["device.installationId"] = new[] { "InstallationId is required." };

            if (string.IsNullOrWhiteSpace(req.Device.Platform))
                errors["device.platform"] = new[] { "Platform is required." };

            if (string.IsNullOrWhiteSpace(req.Device.Os))
                errors["device.os"] = new[] { "OS is required." };

            if (req.Device.DeviceName is { Length: > 64 })
                errors["device.deviceName"] = new[] { "DeviceName max length is 64." };

            if (req.Device.AppVersion is { Length: > 32 })
                errors["device.appVersion"] = new[] { "AppVersion max length is 32." };
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateLogin(LoginRequest req, string normalizedUsername)
    {
        var errors = new Dictionary<string, string[]>();

        if (normalizedUsername.Length < 3 || normalizedUsername.Length > 64)
            errors["username"] = new[] { "Username must be 3-64 characters." };

        if (string.IsNullOrWhiteSpace(req.Password))
            errors["password"] = new[] { "Password is required." };

        if (req.Device is null)
            errors["device"] = new[] { "Device info is required." };
        else
        {
            if (req.Device.InstallationId == Guid.Empty)
                errors["device.installationId"] = new[] { "InstallationId is required." };

            if (string.IsNullOrWhiteSpace(req.Device.Platform))
                errors["device.platform"] = new[] { "Platform is required." };

            if (string.IsNullOrWhiteSpace(req.Device.Os))
                errors["device.os"] = new[] { "OS is required." };

            if (req.Device.DeviceName is { Length: > 64 })
                errors["device.deviceName"] = new[] { "DeviceName max length is 64." };

            if (req.Device.AppVersion is { Length: > 32 })
                errors["device.appVersion"] = new[] { "AppVersion max length is 32." };
        }

        return errors;
    }

    private Result<(Device device, bool created)> TryNormalizeDevice(DeviceInfo info)
    {
        if (info.InstallationId == Guid.Empty)
        {
            return Result<(Device, bool)>.Fail(new AppError(
                Code: "auth.device_invalid",
                Message: "Invalid device info.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["device.installationId"] = new[] { "InstallationId is required." }
                }));
        }

        return Result<(Device, bool)>.Ok(default);
    }

    private async Task<Device> UpsertDeviceAsync(Guid userId, DeviceInfo info, CancellationToken ct)
    {
        var existing = await _db.Devices
            .SingleOrDefaultAsync(d => d.UserId == userId && d.InstallationId == info.InstallationId, ct);

        if (existing is null)
        {
            var d = new Device(userId, info.InstallationId, info.DeviceName, info.Platform, info.Os, info.AppVersion);
            _db.Devices.Add(d);
            return d;
        }

        existing.Rename(string.IsNullOrWhiteSpace(info.DeviceName) ? null : info.DeviceName.Trim());
        existing.Touch(info.AppVersion);
        return existing;
    }

    private async Task EnsureDeviceSettingsAsync(Guid deviceId, CancellationToken ct)
    {
        var ds = await _db.DeviceSettings.FindAsync([deviceId], ct);
        if (ds is null)
            _db.DeviceSettings.Add(new DeviceSettings(deviceId));
    }

    private (string access, string refreshPlain) IssueTokens(User user, Guid deviceId)
    {
        var access = _tokens.CreateAccessToken(user, deviceId);
        var refreshPlain = _refresh.GenerateToken();
        return (access, refreshPlain);
    }
}