namespace IosPhotoImporter.Infrastructure.Wpd;

public sealed class UnsupportedWpdTransport : IWpdTransport
{
    public Task<bool> IsDriverInstalledAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<WpdDeviceSnapshot>> GetConnectedDevicesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WpdDeviceSnapshot>>(Array.Empty<WpdDeviceSnapshot>());
    }

    public async IAsyncEnumerable<WpdMediaObject> EnumerateMediaAsync(string deviceId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }

    public Task<Stream> OpenMediaReadStreamAsync(string deviceId, string sourceObjectId, CancellationToken ct)
    {
        throw new PlatformNotSupportedException("WPD transport is not available. Install Apple Mobile Device Support and run on Windows 11.");
    }
}
