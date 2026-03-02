using System.Collections.Concurrent;
using IosPhotoImporter.Infrastructure.Wpd;

namespace IosPhotoImporter.Infrastructure.Tests.TestDoubles;

public sealed class FakeWpdTransport : IWpdTransport
{
    private readonly ConcurrentDictionary<string, byte[]> _content = new(StringComparer.OrdinalIgnoreCase);

    public bool DriverInstalled { get; set; } = true;

    public List<WpdDeviceSnapshot> Devices { get; } = new();

    public List<WpdMediaObject> MediaObjects { get; } = new();

    public Task<bool> IsDriverInstalledAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(DriverInstalled);
    }

    public Task<IReadOnlyList<WpdDeviceSnapshot>> GetConnectedDevicesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WpdDeviceSnapshot>>(Devices.ToArray());
    }

    public async IAsyncEnumerable<WpdMediaObject> EnumerateMediaAsync(string deviceId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var item in MediaObjects)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }

    public Task<Stream> OpenMediaReadStreamAsync(string deviceId, string sourceObjectId, CancellationToken ct)
    {
        if (!_content.TryGetValue(sourceObjectId, out var bytes))
        {
            throw new FileNotFoundException(sourceObjectId);
        }

        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public void SetContent(string sourceObjectId, byte[] bytes)
    {
        _content[sourceObjectId] = bytes;
    }
}
