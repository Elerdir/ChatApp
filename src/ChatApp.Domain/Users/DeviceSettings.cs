namespace ChatApp.Domain.Users;

public sealed class DeviceSettings
{
    public Guid DeviceId { get; private set; }
    public string SettingsJson { get; private set; } = "{}";
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private DeviceSettings() { }

    public DeviceSettings(Guid deviceId) => DeviceId = deviceId;

    public void Set(string json)
    {
        SettingsJson = json;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}