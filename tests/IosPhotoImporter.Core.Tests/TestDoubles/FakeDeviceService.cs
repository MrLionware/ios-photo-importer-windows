using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Tests.TestDoubles;

public sealed class FakeDeviceService : IDeviceService
{
    public Task<IReadOnlyList<DeviceInfo>> GetConnectedDevicesAsync()
    {
        return Task.FromResult<IReadOnlyList<DeviceInfo>>(new[]
        {
            new DeviceInfo("device-1", "iPhone Test", DeviceConnectionType.Usb, true, true)
        });
    }

    public Task<DeviceHealth> ValidateDeviceReadyAsync(string deviceId)
    {
        return Task.FromResult(new DeviceHealth(DeviceReadinessState.Ready, "ready"));
    }
}
