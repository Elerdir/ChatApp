namespace ChatApp.Domain.Users;

public sealed class UserSettings
{
    public Guid UserId { get; private set; }
    public string SettingsJson { get; private set; } = "{}";
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private UserSettings() { }

    public UserSettings(Guid userId) => UserId = userId;

    public void Set(string json)
    {
        SettingsJson = json;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}