using System.Collections.Concurrent;
using System.Text;
using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;
using IosPhotoImporter.Core.Policies;
using IosPhotoImporter.Core.Services;
using IosPhotoImporter.Core.Tests.TestDoubles;

namespace IosPhotoImporter.Core.Tests.Services;

public sealed class ImportServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "IosPhotoImporter.Core.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StartImportAsync_SkipsLivePhotoMotionComponent()
    {
        Directory.CreateDirectory(_tempRoot);

        var deviceService = new FakeDeviceService();
        var repository = new InMemoryImportStateRepository();
        var discovery = new FakeMediaDiscoveryService(new[]
        {
            new MediaAsset("still", "p1", "IMG_1.HEIC", ".HEIC", 5, DateTimeOffset.UtcNow, MediaKind.Image, false),
            new MediaAsset("motion", null, "IMG_1.MOV", ".MOV", 5, DateTimeOffset.UtcNow, MediaKind.Video, true)
        });

        var content = new FakeMediaContentService(new Dictionary<string, byte[]>
        {
            ["still"] = Encoding.UTF8.GetBytes("still-content"),
            ["motion"] = Encoding.UTF8.GetBytes("motion-content")
        });

        var sut = new ImportService(
            repository,
            deviceService,
            discovery,
            content,
            new SkipIncomingCollisionPolicy());

        var jobId = await sut.StartImportAsync(new ImportRequest("device-1", _tempRoot, ImportMode.NewOnly), CancellationToken.None);

        var result = await WaitForResultAsync(sut, jobId);
        var items = await repository.GetJobItemsAsync(jobId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains(items, x => x.SourceObjectId == "motion" && x.State == ImportItemState.Skipped);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "IMG_1.HEIC")));
    }

    [Fact]
    public async Task ResumeImportAsync_ReprocessesFailedTemporaryItems()
    {
        Directory.CreateDirectory(_tempRoot);

        var deviceService = new FakeDeviceService();
        var repository = new InMemoryImportStateRepository();
        var discovery = new FakeMediaDiscoveryService(new[]
        {
            new MediaAsset("asset-1", null, "IMG_2.HEIC", ".HEIC", 10, DateTimeOffset.UtcNow, MediaKind.Image, false)
        });

        var content = new FlakyMediaContentService("asset-1", failCount: 1, Encoding.UTF8.GetBytes("file-content"));

        var sut = new ImportService(
            repository,
            deviceService,
            discovery,
            content,
            new SkipIncomingCollisionPolicy());

        var jobId = await sut.StartImportAsync(new ImportRequest("device-1", _tempRoot, ImportMode.NewOnly), CancellationToken.None);
        var firstResult = await WaitForResultAsync(sut, jobId);
        Assert.NotNull(firstResult);
        Assert.Equal(1, firstResult.FailedCount);

        await sut.ResumeImportAsync(jobId, CancellationToken.None);
        await WaitForConditionAsync(async () =>
        {
            var items = await repository.GetJobItemsAsync(jobId, CancellationToken.None);
            return items.Any(x => x.SourceObjectId == "asset-1" && x.State == ImportItemState.Completed);
        });
        var resumedResult = await WaitForResultAsync(sut, jobId);

        var items = await repository.GetJobItemsAsync(jobId, CancellationToken.None);

        Assert.NotNull(resumedResult);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "IMG_2.HEIC")));
        Assert.Contains(items, x => x.SourceObjectId == "asset-1" && x.State == ImportItemState.Completed);
    }

    private static async Task<ImportResult?> WaitForResultAsync(IImportService service, ImportJobId jobId)
    {
        var max = TimeSpan.FromSeconds(8);
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < max)
        {
            var result = await service.GetResultAsync(jobId, CancellationToken.None);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(80);
        }

        throw new TimeoutException("Import job did not complete in time during test.");
    }

    private static async Task WaitForConditionAsync(Func<Task<bool>> predicate)
    {
        var max = TimeSpan.FromSeconds(8);
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < max)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(80);
        }

        throw new TimeoutException("Condition was not met in expected time.");
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

    private sealed class FlakyMediaContentService(string targetObjectId, int failCount, byte[] payload) : IMediaContentService
    {
        private int _remainingFailures = failCount;

        public Task<Stream> OpenReadAsync(string deviceId, string sourceObjectId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (sourceObjectId == targetObjectId && _remainingFailures > 0)
            {
                _remainingFailures -= 1;
                throw new IOException("Simulated disconnect.");
            }

            return Task.FromResult<Stream>(new MemoryStream(payload));
        }
    }
}
