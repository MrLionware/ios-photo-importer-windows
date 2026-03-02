using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Abstractions;

public interface IImportStateRepository
{
    Task InitializeAsync(CancellationToken ct);

    Task UpsertDeviceAsync(DeviceInfo deviceInfo, CancellationToken ct);

    Task CreateJobAsync(ImportJob job, CancellationToken ct);

    Task<ImportJob?> GetJobAsync(ImportJobId jobId, CancellationToken ct);

    Task SetJobStatusAsync(ImportJobId jobId, ImportJobStatus status, string? errorMessage, DateTimeOffset? endedAtUtc, CancellationToken ct);

    Task SetCheckpointAsync(ImportJobId jobId, DateTimeOffset checkpointUtc, CancellationToken ct);

    Task UpsertJobItemAsync(ImportJobItem item, CancellationToken ct);

    Task<IReadOnlyList<ImportJobItem>> GetJobItemsAsync(ImportJobId jobId, CancellationToken ct);

    Task<IReadOnlyList<ImportJobItem>> GetJobItemsByStatesAsync(ImportJobId jobId, IReadOnlyCollection<ImportItemState> states, CancellationToken ct);

    Task SetJobItemStateAsync(ImportJobId jobId, string sourceObjectId, ImportItemState state, string? errorCode, string? errorMessage, CancellationToken ct);

    Task<bool> IsPersistentIdImportedAsync(string deviceId, string persistentId, CancellationToken ct);

    Task<bool> IsHashImportedAsync(string deviceId, string hashHex, CancellationToken ct);

    Task MarkImportedAssetAsync(ImportedAssetRecord importedAsset, CancellationToken ct);

    Task ClearHistoryAsync(CancellationToken ct);
}
