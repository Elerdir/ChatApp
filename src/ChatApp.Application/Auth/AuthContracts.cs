namespace ChatApp.Application.Auth;

public sealed record DeviceInfo(Guid InstallationId, string? DeviceName, string Platform, string Os, string? AppVersion);

public sealed record RegisterRequest(string Username, string DisplayName, string Password, DeviceInfo Device);
public sealed record LoginRequest(string Username, string Password, DeviceInfo Device);

public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string Username,
    string DisplayName,
    Guid DeviceId);