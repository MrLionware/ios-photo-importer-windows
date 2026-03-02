using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;
using IosPhotoImporter.Infrastructure.Wpd;

namespace IosPhotoImporter.Infrastructure.Services;

public sealed class WpdDeviceService(IWpdTransport transport) : IDeviceService
{
    public async Task<IReadOnlyList<DeviceInfo>> GetConnectedDevicesAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var snapshots = await transport.GetConnectedDevicesAsync(cts.Token).ConfigureAwait(false);

        return snapshots
            .Select(x => new DeviceInfo(
                x.DeviceId,
                x.DisplayName,
                DeviceConnectionType.Usb,
                x.IsTrusted,
                x.IsReady))
            .ToArray();
    }

    public async Task<DeviceHealth> ValidateDeviceReadyAsync(string deviceId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var isDriverInstalled = await transport.IsDriverInstalledAsync(cts.Token).ConfigureAwait(false);
        if (!isDriverInstalled)
        {
            return new DeviceHealth(
                DeviceReadinessState.MissingDriver,
                "Apple Mobile Device Support is missing. Install Apple Devices or iTunes.");
        }

        var devices = await transport.GetConnectedDevicesAsync(cts.Token).ConfigureAwait(false);
        var target = devices.FirstOrDefault(x => x.DeviceId == deviceId);
        if (target is null)
        {
            return new DeviceHealth(DeviceReadinessState.NotConnected, "Device is not connected.");
        }

        if (!target.IsTrusted)
        {
            return new DeviceHealth(DeviceReadinessState.Untrusted, "Device is not trusted. Unlock iPhone and tap Trust.");
        }

        if (!target.IsReady)
        {
            return new DeviceHealth(DeviceReadinessState.DeviceLocked, "Device is locked or busy. Unlock iPhone and keep screen on.");
        }

        return new DeviceHealth(DeviceReadinessState.Ready, "Device is ready for import.");
    }
}
