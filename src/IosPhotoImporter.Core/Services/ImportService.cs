using System.Collections.Concurrent;
using System.Security.Cryptography;
using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IosPhotoImporter.Core.Services;

public sealed class ImportService(
    IImportStateRepository repository,
    IDeviceService deviceService,
    IMediaDiscoveryService mediaDiscoveryService,
    IMediaContentService mediaContentService,
    IFileCollisionPolicy fileCollisionPolicy,
    ILogger<ImportService>? logger = null) : IImportService
{
    private readonly ConcurrentDictionary<ImportJobId, CancellationTokenSource> _runningJobs = new();
    private readonly ConcurrentDictionary<ImportJobId, ImportResult> _results = new();
    private readonly ILogger<ImportService> _logger = logger ?? NullLogger<ImportService>.Instance;

    public event EventHandler<ImportProgress>? ProgressChanged;

    public async Task<ImportJobId> StartImportAsync(ImportRequest request, CancellationToken ct)
    {
        ValidateRequest(request);
        await repository.InitializeAsync(ct).ConfigureAwait(false);

        var health = await deviceService.ValidateDeviceReadyAsync(request.DeviceId).ConfigureAwait(false);
        if (!health.IsReady)
        {
            throw new InvalidOperationException($"Device is not ready: {health.State} ({health.Message})");
        }

        var devices = await deviceService.GetConnectedDevicesAsync().ConfigureAwait(false);
        var selectedDevice = devices.FirstOrDefault(x => x.DeviceId == request.DeviceId)
            ?? throw new InvalidOperationException("Selected device is no longer connected.");

        await repository.UpsertDeviceAsync(selectedDevice, ct).ConfigureAwait(false);

        var jobId = ImportJobId.New();
        var startedAt = DateTimeOffset.UtcNow;

        var job = new ImportJob(
            jobId,
            request.DeviceId,
            request.DestinationPath,
            ImportJobStatus.Pending,
            startedAt,
            null,
            startedAt,
            null);

        await repository.CreateJobAsync(job, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Import job {JobId} created for device {DeviceId}. Destination={Destination}.",
            jobId,
            request.DeviceId,
            request.DestinationPath);

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (!_runningJobs.TryAdd(jobId, linkedCts))
        {
            linkedCts.Dispose();
            throw new InvalidOperationException("Unable to schedule import job.");
        }

        _ = Task.Run(() => ExecuteStartImportAsync(job, linkedCts.Token), CancellationToken.None)
            .ContinueWith(_ =>
            {
                if (_runningJobs.TryRemove(jobId, out var cts))
                {
                    cts.Dispose();
                }
            }, TaskScheduler.Default);

        return jobId;
    }

    public async Task ResumeImportAsync(ImportJobId jobId, CancellationToken ct)
    {
        await repository.InitializeAsync(ct).ConfigureAwait(false);
        var job = await repository.GetJobAsync(jobId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Job {jobId} not found.");

        if (job.Status is ImportJobStatus.Completed or ImportJobStatus.Cancelled)
        {
            return;
        }

        var health = await deviceService.ValidateDeviceReadyAsync(job.DeviceId).ConfigureAwait(false);
        if (!health.IsReady)
        {
            throw new InvalidOperationException($"Device is not ready: {health.State} ({health.Message})");
        }

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (!_runningJobs.TryAdd(jobId, linkedCts))
        {
            throw new InvalidOperationException("Job is already running.");
        }

        _ = Task.Run(() => ExecuteResumeImportAsync(job, linkedCts.Token), CancellationToken.None)
            .ContinueWith(_ =>
            {
                if (_runningJobs.TryRemove(jobId, out var cts))
                {
                    cts.Dispose();
                }
            }, TaskScheduler.Default);
    }

    public Task CancelImportAsync(ImportJobId jobId)
    {
        if (_runningJobs.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancellation requested for job {JobId}.", jobId);
        }

        return Task.CompletedTask;
    }

    public Task<ImportResult?> GetResultAsync(ImportJobId jobId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _results.TryGetValue(jobId, out var result);
        return Task.FromResult(result);
    }

    private async Task ExecuteStartImportAsync(ImportJob job, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var counts = new ProgressAccumulator();

        try
        {
            await repository.SetJobStatusAsync(job.JobId, ImportJobStatus.Running, null, null, ct).ConfigureAwait(false);
            Directory.CreateDirectory(job.DestinationPath);
            CleanupStaleTempFiles(job.DestinationPath);
            _logger.LogInformation(
                "Job {JobId} started. Device={DeviceId}, Destination={Destination}.",
                job.JobId,
                job.DeviceId,
                job.DestinationPath);

            var assets = new List<MediaAsset>();
            await foreach (var asset in mediaDiscoveryService.EnumerateAssetsAsync(job.DeviceId, ct).ConfigureAwait(false))
            {
                assets.Add(asset);
                counts.Total = assets.Count;
                EmitProgress(job.JobId, counts, $"Scanning: {asset.Name}");
            }

            foreach (var asset in assets)
            {
                await repository.UpsertJobItemAsync(new ImportJobItem(job.JobId, asset.SourceObjectId, ImportItemState.Pending), ct)
                    .ConfigureAwait(false);
            }

            await ProcessAssetsAsync(job, assets, counts, ct).ConfigureAwait(false);

            var status = counts.Failed > 0
                ? ImportJobStatus.Failed
                : ImportJobStatus.Completed;

            await FinalizeJobAsync(job.JobId, counts, startedAt, status, null).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Job {JobId} cancelled by user.", job.JobId);
            await FinalizeJobAsync(job.JobId, counts, startedAt, ImportJobStatus.Cancelled, "Cancelled by user.")
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed unexpectedly during startup or scan.", job.JobId);
            counts.Failed = Math.Max(1, counts.Failed);
            await FinalizeJobAsync(job.JobId, counts, startedAt, ImportJobStatus.Failed, ex.Message)
                .ConfigureAwait(false);
        }
    }

    private async Task ExecuteResumeImportAsync(ImportJob job, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var counts = new ProgressAccumulator();

        try
        {
            await repository.SetJobStatusAsync(job.JobId, ImportJobStatus.Running, null, null, ct).ConfigureAwait(false);
            Directory.CreateDirectory(job.DestinationPath);
            CleanupStaleTempFiles(job.DestinationPath);
            _logger.LogInformation("Resuming job {JobId}.", job.JobId);

            var pendingStates = new[] { ImportItemState.Pending, ImportItemState.FailedTemporary };
            var pendingItems = await repository.GetJobItemsByStatesAsync(job.JobId, pendingStates, ct).ConfigureAwait(false);
            var pendingIds = pendingItems.Select(x => x.SourceObjectId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var assetLookup = new Dictionary<string, MediaAsset>(StringComparer.OrdinalIgnoreCase);
            await foreach (var asset in mediaDiscoveryService.EnumerateAssetsAsync(job.DeviceId, ct).ConfigureAwait(false))
            {
                if (pendingIds.Contains(asset.SourceObjectId))
                {
                    assetLookup[asset.SourceObjectId] = asset;
                }
            }

            var assetsToProcess = pendingItems
                .Select(x => assetLookup.TryGetValue(x.SourceObjectId, out var asset) ? asset : null)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();

            foreach (var missing in pendingItems.Where(x => !assetLookup.ContainsKey(x.SourceObjectId)))
            {
                counts.Failed += 1;
                await repository.SetJobItemStateAsync(
                        job.JobId,
                        missing.SourceObjectId,
                        ImportItemState.FailedTemporary,
                        "SOURCE_MISSING",
                        "Asset unavailable on device during resume.",
                        ct)
                    .ConfigureAwait(false);
            }

            counts.Total = pendingItems.Count;
            await ProcessAssetsAsync(job, assetsToProcess, counts, ct).ConfigureAwait(false);

            var status = counts.Failed > 0
                ? ImportJobStatus.Failed
                : ImportJobStatus.Completed;

            await FinalizeJobAsync(job.JobId, counts, startedAt, status, null).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Resume cancelled for job {JobId}.", job.JobId);
            await FinalizeJobAsync(job.JobId, counts, startedAt, ImportJobStatus.Cancelled, "Cancelled by user.")
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed unexpectedly during resume.", job.JobId);
            counts.Failed = Math.Max(1, counts.Failed);
            await FinalizeJobAsync(job.JobId, counts, startedAt, ImportJobStatus.Failed, ex.Message)
                .ConfigureAwait(false);
        }
    }

    private async Task ProcessAssetsAsync(
        ImportJob job,
        IReadOnlyList<MediaAsset> assets,
        ProgressAccumulator counts,
        CancellationToken ct)
    {
        foreach (var asset in assets)
        {
            await ProcessAssetAsync(job, asset, counts, ct).ConfigureAwait(false);
        }
    }

    private async Task ProcessAssetAsync(
        ImportJob job,
        MediaAsset asset,
        ProgressAccumulator counts,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await repository.SetJobItemStateAsync(job.JobId, asset.SourceObjectId, ImportItemState.Processing, null, null, ct)
            .ConfigureAwait(false);
        EmitProgress(job.JobId, counts, asset.Name);

        if (asset.MediaKind == MediaKind.Unsupported)
        {
            await MarkSkippedAsync(job, asset, counts, "UNSUPPORTED_MEDIA", "Unsupported media type.", ct).ConfigureAwait(false);
            return;
        }

        if (asset.IsLivePhotoMotionComponent)
        {
            await MarkSkippedAsync(job, asset, counts, "LIVE_MOTION_SKIPPED", "Live Photo motion component skipped in v1.", ct).ConfigureAwait(false);
            return;
        }

        var finalPath = Path.Combine(job.DestinationPath, asset.Name);
        var finalPathExists = File.Exists(finalPath);
        var collisionAction = fileCollisionPolicy.Resolve(finalPath, finalPathExists);
        if (collisionAction == FileCollisionAction.Skip)
        {
            await MarkSkippedAsync(job, asset, counts, "FILENAME_COLLISION", "Destination file already exists.", ct).ConfigureAwait(false);
            return;
        }

        var tempPath = Path.Combine(job.DestinationPath, $"{asset.Name}.{Guid.NewGuid():N}.part");

        try
        {
            using var sourceStream = await mediaContentService.OpenReadAsync(job.DeviceId, asset.SourceObjectId, ct).ConfigureAwait(false);
            await using var tempFileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            var hashHex = await CopyWithHashAsync(sourceStream, tempFileStream, ct).ConfigureAwait(false);
            await tempFileStream.FlushAsync(ct).ConfigureAwait(false);

            tempFileStream.Close();
            File.Move(tempPath, finalPath);

            await repository.MarkImportedAssetAsync(
                new ImportedAssetRecord(
                    job.DeviceId,
                    asset.PersistentId,
                    asset.SourceObjectId,
                    asset.Name,
                    asset.SizeBytes,
                    hashHex,
                    finalPath,
                    DateTimeOffset.UtcNow),
                ct).ConfigureAwait(false);

            await repository.SetJobItemStateAsync(job.JobId, asset.SourceObjectId, ImportItemState.Completed, null, null, ct)
                .ConfigureAwait(false);

            counts.Completed += 1;
            counts.BytesTransferred += asset.SizeBytes;
            await repository.SetCheckpointAsync(job.JobId, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            EmitProgress(job.JobId, counts, asset.Name);
        }
        catch (OperationCanceledException)
        {
            SafeDeleteTemp(tempPath);
            await repository.SetJobItemStateAsync(job.JobId, asset.SourceObjectId, ImportItemState.FailedTemporary, "CANCELLED", "Cancelled by user.", CancellationToken.None)
                .ConfigureAwait(false);
            _logger.LogWarning(
                "Copy cancelled for job {JobId}. ObjectId={ObjectId}, File={FileName}.",
                job.JobId,
                asset.SourceObjectId,
                asset.Name);
            throw;
        }
        catch (IOException ioEx)
        {
            SafeDeleteTemp(tempPath);
            counts.Failed += 1;
            await repository.SetJobItemStateAsync(job.JobId, asset.SourceObjectId, ImportItemState.FailedTemporary, "IO_ERROR", ioEx.Message, ct)
                .ConfigureAwait(false);
            await repository.SetCheckpointAsync(job.JobId, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            EmitProgress(job.JobId, counts, asset.Name);
            _logger.LogWarning(
                ioEx,
                "I/O failure on job {JobId}. ObjectId={ObjectId}, File={FileName}.",
                job.JobId,
                asset.SourceObjectId,
                asset.Name);
        }
        catch (Exception ex)
        {
            SafeDeleteTemp(tempPath);
            counts.Failed += 1;
            await repository.SetJobItemStateAsync(job.JobId, asset.SourceObjectId, ImportItemState.Failed, "UNEXPECTED", ex.Message, ct)
                .ConfigureAwait(false);
            await repository.SetCheckpointAsync(job.JobId, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            EmitProgress(job.JobId, counts, asset.Name);
            _logger.LogError(
                ex,
                "Unexpected failure on job {JobId}. ObjectId={ObjectId}, File={FileName}.",
                job.JobId,
                asset.SourceObjectId,
                asset.Name);
        }
    }

    private async Task MarkSkippedAsync(
        ImportJob job,
        MediaAsset asset,
        ProgressAccumulator counts,
        string errorCode,
        string errorMessage,
        CancellationToken ct)
    {
        counts.Skipped += 1;
        await repository.SetJobItemStateAsync(job.JobId, asset.SourceObjectId, ImportItemState.Skipped, errorCode, errorMessage, ct)
            .ConfigureAwait(false);
        await repository.SetCheckpointAsync(job.JobId, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        EmitProgress(job.JobId, counts, asset.Name);
        _logger.LogWarning(
            "Skipped item on job {JobId}. ObjectId={ObjectId}, File={FileName}, Code={Code}, Message={Message}.",
            job.JobId,
            asset.SourceObjectId,
            asset.Name,
            errorCode,
            errorMessage);
    }

    private void EmitProgress(ImportJobId jobId, ProgressAccumulator counts, string? currentFile)
    {
        ProgressChanged?.Invoke(
            this,
            new ImportProgress(
                jobId,
                counts.Total,
                counts.Completed,
                counts.Skipped,
                counts.Failed,
                counts.BytesTransferred,
                currentFile));
    }

    private async Task FinalizeJobAsync(
        ImportJobId jobId,
        ProgressAccumulator counts,
        DateTimeOffset startedAt,
        ImportJobStatus status,
        string? errorMessage)
    {
        _results[jobId] = new ImportResult(
            counts.Completed,
            counts.Skipped,
            counts.Failed,
            DateTimeOffset.UtcNow - startedAt,
            counts.BytesTransferred);

        await repository.SetJobStatusAsync(jobId, status, errorMessage, DateTimeOffset.UtcNow, CancellationToken.None)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Job {JobId} finished. Status={Status}, Imported={Imported}, Skipped={Skipped}, Failed={Failed}, Bytes={Bytes}, Error={Error}.",
            jobId,
            status,
            counts.Completed,
            counts.Skipped,
            counts.Failed,
            counts.BytesTransferred,
            errorMessage ?? string.Empty);

        EmitProgress(jobId, counts, currentFile: null);
    }

    private static async Task<string> CopyWithHashAsync(Stream source, Stream destination, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        var buffer = new byte[1024 * 128];

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash!);
    }

    private static void SafeDeleteTemp(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // Best-effort cleanup of temp files.
        }
    }

    private static void CleanupStaleTempFiles(string destinationPath)
    {
        if (!Directory.Exists(destinationPath))
        {
            return;
        }

        try
        {
            foreach (var tempPath in Directory.EnumerateFiles(destinationPath, "*.part", SearchOption.TopDirectoryOnly))
            {
                SafeDeleteTemp(tempPath);
            }
        }
        catch
        {
            // Best-effort cleanup of temp files.
        }
    }

    private static void ValidateRequest(ImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new ArgumentException("Device id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DestinationPath))
        {
            throw new ArgumentException("Destination path is required.", nameof(request));
        }

        if (request.ImportMode != ImportMode.NewOnly)
        {
            throw new NotSupportedException("Only NewOnly import mode is supported in v1.");
        }
    }

    private sealed class ProgressAccumulator
    {
        public int Total { get; set; }

        public int Completed { get; set; }

        public int Skipped { get; set; }

        public int Failed { get; set; }

        public long BytesTransferred { get; set; }
    }
}
