using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Infrastructure.Wpd;

namespace IosPhotoImporter.Infrastructure.Services;

public sealed class WpdMediaContentService(IWpdTransport transport) : IMediaContentService
{
    public Task<Stream> OpenReadAsync(string deviceId, string sourceObjectId, CancellationToken ct)
    {
        return transport.OpenMediaReadStreamAsync(deviceId, sourceObjectId, ct);
    }
}
