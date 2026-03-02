using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Abstractions;

public interface IDeviceService
{
    Task<IReadOnlyList<DeviceInfo>> GetConnectedDevicesAsync();

    Task<DeviceHealth> ValidateDeviceReadyAsync(string deviceId);
}
