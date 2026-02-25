namespace ChatApp.Domain.Users;

public sealed class UserSettingsGlobal
{
    public Guid UserId { get; set; }

    // JSON blob – serialized dictionary<string,string>
    public string SettingsJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAt { get; set; }

    // navigation property (optional but recommended)
    public User User { get; set; } = default!;
}