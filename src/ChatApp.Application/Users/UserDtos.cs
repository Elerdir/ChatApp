namespace ChatApp.Application.Users;

public sealed record UserSearchItemDto(
    Guid UserId,
    string Username,
    string DisplayName,
    Guid? AvatarFileId);

public sealed record UserCardDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? Bio,
    Guid? AvatarFileId);