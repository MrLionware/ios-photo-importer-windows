using System.Collections.Concurrent;
using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Tests.TestDoubles;

public sealed class InMemoryImportStateRepository : IImportStateRepository
{
    private readonly ConcurrentDictionary<string, DeviceInfo> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<ImportJobId, ImportJob> _jobs = new();
    private readonly ConcurrentDictionary<(ImportJobId JobId, string ObjectId), ImportJobItem> _jobItems = new();
    private readonly List<ImportedAssetRecord> _importedAssets = new();

    public bool PersistentIdExists { get; set; }

    public bool HashExists { get; set; }

    public Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task UpsertDeviceAsync(DeviceInfo deviceInfo, CancellationToken ct)
    {
        _devices[deviceInfo.DeviceId] = deviceInfo;
        return Task.CompletedTask;
    }

    public Task CreateJobAsync(ImportJob job, CancellationToken ct)
    {
        _jobs[job.JobId] = job;
        return Task.CompletedTask;
    }

    public Task<ImportJob?> GetJobAsync(ImportJobId jobId, CancellationToken ct)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task SetJobStatusAsync(ImportJobId jobId, ImportJobStatus status, string? errorMessage, DateTimeOffset? endedAtUtc, CancellationToken ct)
    {
        if (_jobs.TryGetValue(jobId, out var existing))
        {
            _jobs[jobId] = existing with { Status = status, EndedAtUtc = endedAtUtc, LastError = errorMessage };
        }

        return Task.CompletedTask;
    }

    public Task SetCheckpointAsync(ImportJobId jobId, DateTimeOffset checkpointUtc, CancellationToken ct)
    {
        if (_jobs.TryGetValue(jobId, out var existing))
        {
            _jobs[jobId] = existing with { LastCheckpointUtc = checkpointUtc };
        }

        return Task.CompletedTask;
    }

    public Task UpsertJobItemAsync(ImportJobItem item, CancellationToken ct)
    {
        _jobItems[(item.JobId, item.SourceObjectId)] = item;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ImportJobItem>> GetJobItemsAsync(ImportJobId jobId, CancellationToken ct)
    {
        var items = _jobItems.Values.Where(x => x.JobId == jobId).ToArray();
        return Task.FromResult<IReadOnlyList<ImportJobItem>>(items);
    }

    public Task<IReadOnlyList<ImportJobItem>> GetJobItemsByStatesAsync(ImportJobId jobId, IReadOnlyCollection<ImportItemState> states, CancellationToken ct)
    {
        var items = _jobItems.Values
            .Where(x => x.JobId == jobId && states.Contains(x.State))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ImportJobItem>>(items);
    }

    public Task SetJobItemStateAsync(ImportJobId jobId, string sourceObjectId, ImportItemState state, string? errorCode, string? errorMessage, CancellationToken ct)
    {
        var key = (jobId, sourceObjectId);
        _jobItems.TryGetValue(key, out var existing);
        existing ??= new ImportJobItem(jobId, sourceObjectId, state, errorCode, errorMessage);
        _jobItems[key] = existing with { State = state, ErrorCode = errorCode, ErrorMessage = errorMessage };
        return Task.CompletedTask;
    }

    public Task<bool> IsPersistentIdImportedAsync(string deviceId, string persistentId, CancellationToken ct)
    {
        var exists = PersistentIdExists || _importedAssets.Any(x => x.DeviceId == deviceId && x.PersistentId == persistentId);
        return Task.FromResult(exists);
    }

    public Task<bool> IsHashImportedAsync(string deviceId, string hashHex, CancellationToken ct)
    {
        var exists = HashExists || _importedAssets.Any(x => x.DeviceId == deviceId && x.Sha256 == hashHex);
        return Task.FromResult(exists);
    }

    public Task MarkImportedAssetAsync(ImportedAssetRecord importedAsset, CancellationToken ct)
    {
        _importedAssets.Add(importedAsset);
        return Task.CompletedTask;
    }

    public Task ClearHistoryAsync(CancellationToken ct)
    {
        _jobs.Clear();
        _jobItems.Clear();
        _importedAssets.Clear();
        return Task.CompletedTask;
    }
}
