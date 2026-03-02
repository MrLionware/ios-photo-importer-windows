using IosPhotoImporter.Core.Abstractions;

namespace IosPhotoImporter.Core.Tests.TestDoubles;

public sealed class FakeMediaContentService(IReadOnlyDictionary<string, byte[]> contentMap) : IMediaContentService
{
    public Task<Stream> OpenReadAsync(string deviceId, string sourceObjectId, CancellationToken ct)
    {
        if (!contentMap.TryGetValue(sourceObjectId, out var bytes))
        {
            throw new FileNotFoundException($"Missing mocked object content: {sourceObjectId}");
        }

        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}
