namespace ChatApp.Domain.Users;

public sealed class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Username { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private User() { }

    public User(string username, string displayName, string passwordHash)
    {
        Username = username.Trim();
        DisplayName = displayName.Trim();
        PasswordHash = passwordHash;
    }

    public void ChangeDisplayName(string displayName) => DisplayName = displayName.Trim();
}