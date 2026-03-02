using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;
using IosPhotoImporter.Infrastructure.Wpd;

namespace IosPhotoImporter.Infrastructure.Services;

public sealed class WpdMediaDiscoveryService(IWpdTransport transport) : IMediaDiscoveryService
{
    public async IAsyncEnumerable<MediaAsset> EnumerateAssetsAsync(
        string deviceId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var media in transport.EnumerateMediaAsync(deviceId, ct).ConfigureAwait(false))
        {
            yield return new MediaAsset(
                media.SourceObjectId,
                media.PersistentId,
                media.Name,
                media.Extension,
                media.SizeBytes,
                media.CreatedAt,
                media.MediaKind,
                media.IsLivePhotoMotionComponent);
        }
    }
}
