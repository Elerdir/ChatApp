namespace ChatApp.Application.Users;

public sealed record UserMeDto(
    Guid UserId,
    string Username,
    string? DisplayName,
    string? AvatarUrl
);