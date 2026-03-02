namespace IosPhotoImporter.Core.Models;

public sealed record DeviceInfo(
    string DeviceId,
    string Name,
    DeviceConnectionType ConnectionType,
    bool IsTrusted,
    bool IsReady);
