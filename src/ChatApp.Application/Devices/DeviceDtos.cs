namespace ChatApp.Application.Devices;

public sealed record DeviceDto(
    Guid DeviceId,
    Guid InstallationId,
    string? DeviceName,
    string Platform,
    string Os,
    string? AppVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt);

public sealed record RenameDeviceRequest(string? DeviceName);