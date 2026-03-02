using IosPhotoImporter.Core.Models;
using IosPhotoImporter.Infrastructure.Data;

namespace IosPhotoImporter.Infrastructure.Tests.Data;

public sealed class SqliteImportStateRepositoryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "IosPhotoImporter.Infrastructure.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Repository_PersistsJobItemsAndImportedAssets()
    {
        Directory.CreateDirectory(_tempRoot);
        var dbPath = Path.Combine(_tempRoot, "state.db");
        var existingImportPath = Path.Combine(_tempRoot, "IMG_1.HEIC");
        await File.WriteAllTextAsync(existingImportPath, "test-content");
        var repository = new SqliteImportStateRepository(new SqliteRepositoryOptions(dbPath));

        await repository.InitializeAsync(CancellationToken.None);

        var device = new DeviceInfo("device-1", "iPhone", DeviceConnectionType.Usb, true, true);
        await repository.UpsertDeviceAsync(device, CancellationToken.None);

        var jobId = ImportJobId.New();
        await repository.CreateJobAsync(
            new ImportJob(
                jobId,
                "device-1",
                "C:\\Imports",
                ImportJobStatus.Pending,
                DateTimeOffset.UtcNow,
                null,
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        await repository.UpsertJobItemAsync(new ImportJobItem(jobId, "obj-1", ImportItemState.Pending), CancellationToken.None);
        await repository.SetJobItemStateAsync(jobId, "obj-1", ImportItemState.Completed, null, null, CancellationToken.None);

        await repository.MarkImportedAssetAsync(
            new ImportedAssetRecord("device-1", "pid-1", "obj-1", "IMG_1.HEIC", 1024, "HASH", existingImportPath, DateTimeOffset.UtcNow),
            CancellationToken.None);

        var job = await repository.GetJobAsync(jobId, CancellationToken.None);
        var items = await repository.GetJobItemsAsync(jobId, CancellationToken.None);
        var byPid = await repository.IsPersistentIdImportedAsync("device-1", "pid-1", CancellationToken.None);
        var byHash = await repository.IsHashImportedAsync("device-1", "HASH", CancellationToken.None);

        Assert.NotNull(job);
        Assert.Single(items);
        Assert.Equal(ImportItemState.Completed, items[0].State);
        Assert.True(byPid);
        Assert.True(byHash);

        await repository.ClearHistoryAsync(CancellationToken.None);
        var itemsAfterClear = await repository.GetJobItemsAsync(jobId, CancellationToken.None);
        var byPidAfterClear = await repository.IsPersistentIdImportedAsync("device-1", "pid-1", CancellationToken.None);

        Assert.Empty(itemsAfterClear);
        Assert.False(byPidAfterClear);
    }

    [Fact]
    public async Task DuplicateChecks_ReturnFalseWhenTrackedLocalFileIsMissing()
    {
        Directory.CreateDirectory(_tempRoot);
        var dbPath = Path.Combine(_tempRoot, "state.db");
        var missingPath = Path.Combine(_tempRoot, "missing.HEIC");
        var repository = new SqliteImportStateRepository(new SqliteRepositoryOptions(dbPath));

        await repository.InitializeAsync(CancellationToken.None);
        await repository.MarkImportedAssetAsync(
            new ImportedAssetRecord("device-1", "pid-missing", "obj-missing", "missing.HEIC", 50, "HASH-MISSING", missingPath, DateTimeOffset.UtcNow),
            CancellationToken.None);

        var byPid = await repository.IsPersistentIdImportedAsync("device-1", "pid-missing", CancellationToken.None);
        var byHash = await repository.IsHashImportedAsync("device-1", "HASH-MISSING", CancellationToken.None);

        Assert.False(byPid);
        Assert.False(byHash);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // no-op
        }
    }
}
