namespace ChatApp.Domain.Auth;

public sealed class Device
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }

    public Guid InstallationId { get; private set; } // stable per install
    public string? DeviceName { get; private set; }
    public string Platform { get; private set; } = default!; // desktop|mobile
    public string Os { get; private set; } = default!;       // windows|linux|macos|android|ios
    public string? AppVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; private set; } = DateTimeOffset.UtcNow;

    private Device() { }

    public Device(Guid userId, Guid installationId, string? deviceName, string platform, string os, string? appVersion)
    {
        UserId = userId;
        InstallationId = installationId;
        DeviceName = deviceName;
        Platform = platform.Trim().ToLowerInvariant();
        Os = os.Trim().ToLowerInvariant();
        AppVersion = appVersion;
    }

    public void Touch(string? appVersion)
    {
        LastSeenAt = DateTimeOffset.UtcNow;
        AppVersion = appVersion ?? AppVersion;
    }

    public void Rename(string? deviceName) => DeviceName = deviceName;
}