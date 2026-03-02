using IosPhotoImporter.Core.Models;
using IosPhotoImporter.Infrastructure.Services;
using IosPhotoImporter.Infrastructure.Tests.TestDoubles;
using IosPhotoImporter.Infrastructure.Wpd;

namespace IosPhotoImporter.Infrastructure.Tests.Services;

public sealed class WpdDeviceServiceTests
{
    [Fact]
    public async Task ValidateDeviceReadyAsync_ReturnsMissingDriver_WhenDriverUnavailable()
    {
        var transport = new FakeWpdTransport { DriverInstalled = false };
        var sut = new WpdDeviceService(transport);

        var health = await sut.ValidateDeviceReadyAsync("device-1");

        Assert.Equal(DeviceReadinessState.MissingDriver, health.State);
    }

    [Fact]
    public async Task ValidateDeviceReadyAsync_ReturnsReady_WhenTrustedAndReady()
    {
        var transport = new FakeWpdTransport();
        transport.Devices.Add(new WpdDeviceSnapshot("device-1", "iPhone", true, true));

        var sut = new WpdDeviceService(transport);
        var health = await sut.ValidateDeviceReadyAsync("device-1");

        Assert.Equal(DeviceReadinessState.Ready, health.State);
        Assert.True(health.IsReady);
    }
}
