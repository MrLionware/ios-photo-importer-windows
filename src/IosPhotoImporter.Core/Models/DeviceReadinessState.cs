namespace IosPhotoImporter.Core.Models;

public enum DeviceReadinessState
{
    Ready = 1,
    MissingDriver = 2,
    DeviceLocked = 3,
    Untrusted = 4,
    NotConnected = 5,
    UnknownError = 6
}
