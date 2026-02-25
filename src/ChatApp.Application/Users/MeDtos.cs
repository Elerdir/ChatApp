using System.Text.Json;

namespace ChatApp.Application.Users;


public sealed record MeDto(Guid UserId, string Username, string DisplayName, string? Bio, Guid? AvatarFileId);

public sealed record PatchProfileRequest(string DisplayName, string? Bio);

public sealed record SettingsDto(string Global, string Device, string Effective);

// Patch je libovolný JSON objekt
public sealed record PatchSettingsRequest(JsonElement Patch);