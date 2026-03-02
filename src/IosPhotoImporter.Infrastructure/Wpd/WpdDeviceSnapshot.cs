namespace IosPhotoImporter.Infrastructure.Wpd;

public sealed record WpdDeviceSnapshot(
    string DeviceId,
    string DisplayName,
    bool IsTrusted,
    bool IsReady);
