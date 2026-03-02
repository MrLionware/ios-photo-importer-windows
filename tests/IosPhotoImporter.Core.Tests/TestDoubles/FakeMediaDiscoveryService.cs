using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Tests.TestDoubles;

public sealed class FakeMediaDiscoveryService(IEnumerable<MediaAsset> assets) : IMediaDiscoveryService
{
    public async IAsyncEnumerable<MediaAsset> EnumerateAssetsAsync(string deviceId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var asset in assets)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return asset;
        }
    }
}
