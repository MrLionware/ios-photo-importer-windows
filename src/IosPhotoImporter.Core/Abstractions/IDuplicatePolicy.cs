using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Abstractions;

public interface IDuplicatePolicy
{
    Task<DuplicateCheckResult> CheckAsync(
        string deviceId,
        MediaAsset asset,
        Func<CancellationToken, Task<string>> hashFactory,
        CancellationToken ct);
}
