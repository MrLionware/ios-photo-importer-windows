namespace IosPhotoImporter.Infrastructure.Wpd;

public interface IWpdTransport
{
    Task<bool> IsDriverInstalledAsync(CancellationToken ct);

    Task<IReadOnlyList<WpdDeviceSnapshot>> GetConnectedDevicesAsync(CancellationToken ct);

    IAsyncEnumerable<WpdMediaObject> EnumerateMediaAsync(string deviceId, CancellationToken ct);

    Task<Stream> OpenMediaReadStreamAsync(string deviceId, string sourceObjectId, CancellationToken ct);
}
