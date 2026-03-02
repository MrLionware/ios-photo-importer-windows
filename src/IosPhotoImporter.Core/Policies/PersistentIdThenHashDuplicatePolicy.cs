using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Policies;

public sealed class PersistentIdThenHashDuplicatePolicy(IImportStateRepository repository) : IDuplicatePolicy
{
    public async Task<DuplicateCheckResult> CheckAsync(
        string deviceId,
        MediaAsset asset,
        Func<CancellationToken, Task<string>> hashFactory,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(asset.PersistentId))
        {
            var isDuplicateByPersistentId = await repository.IsPersistentIdImportedAsync(deviceId, asset.PersistentId, ct).ConfigureAwait(false);
            if (isDuplicateByPersistentId)
            {
                return DuplicateCheckResult.Duplicate("Duplicate by persistent id");
            }

            return DuplicateCheckResult.NotDuplicate();
        }

        var hashHex = await hashFactory(ct).ConfigureAwait(false);
        var isDuplicateByHash = await repository.IsHashImportedAsync(deviceId, hashHex, ct).ConfigureAwait(false);
        return isDuplicateByHash
            ? DuplicateCheckResult.Duplicate("Duplicate by hash")
            : DuplicateCheckResult.NotDuplicate(hashHex);
    }
}
