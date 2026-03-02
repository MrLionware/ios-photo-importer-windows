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
        }

        var hashHex = await hashFactory(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(hashHex))
        {
            return DuplicateCheckResult.NotDuplicate();
        }

        var isDuplicateByHash = await repository.IsHashImportedAsync(deviceId, hashHex, ct).ConfigureAwait(false);
        return isDuplicateByHash
            ? DuplicateCheckResult.Duplicate("Duplicate by hash")
            : DuplicateCheckResult.NotDuplicate(hashHex);
    }
}
