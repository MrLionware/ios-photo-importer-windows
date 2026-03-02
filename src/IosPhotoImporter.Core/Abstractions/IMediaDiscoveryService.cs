using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Abstractions;

public interface IMediaDiscoveryService
{
    IAsyncEnumerable<MediaAsset> EnumerateAssetsAsync(string deviceId, CancellationToken ct);
}
