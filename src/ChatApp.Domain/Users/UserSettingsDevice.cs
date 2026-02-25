namespace ChatApp.Domain.Users;

public sealed class UserSettingsDevice
{
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }

    // JSON blob – serialized dictionary<string,string>
    public string SettingsJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAt { get; set; }

    // navigation (optional)
    public User User { get; set; } = default!;
    // Device entity navigation můžeš mít, ale není nutná
}