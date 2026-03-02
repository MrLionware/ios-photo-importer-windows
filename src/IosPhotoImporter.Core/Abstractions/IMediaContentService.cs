namespace IosPhotoImporter.Core.Abstractions;

public interface IMediaContentService
{
    Task<Stream> OpenReadAsync(string deviceId, string sourceObjectId, CancellationToken ct);
}
