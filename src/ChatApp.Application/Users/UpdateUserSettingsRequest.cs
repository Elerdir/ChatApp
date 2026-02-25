namespace ChatApp.Application.Users;

public sealed record UpdateUserSettingsRequest(
    string Scope,
    IReadOnlyDictionary<string, string> Settings);