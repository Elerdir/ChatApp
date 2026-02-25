namespace ChatApp.Domain.Auth;

public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid DeviceId { get; private set; }

    public string TokenHash { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }

    public RefreshToken(Guid userId, Guid deviceId, string tokenHash, DateTimeOffset expiresAt)
    {
        UserId = userId;
        DeviceId = deviceId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    public void Revoke(Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null) return;
        RevokedAt = DateTimeOffset.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }
}