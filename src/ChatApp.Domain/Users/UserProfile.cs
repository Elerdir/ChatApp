namespace ChatApp.Domain.Users;

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = default!;
    public string? Bio { get; private set; }
    public Guid? AvatarFileId { get; private set; }
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private UserProfile() { }


    public void Update(string displayName, string? bio)
    {
        DisplayName = displayName.Trim();
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetAvatar(Guid? fileId)
    {
        AvatarFileId = fileId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}