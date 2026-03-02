using System.Text;
using IosPhotoImporter.Core.Models;
using IosPhotoImporter.Core.Policies;
using IosPhotoImporter.Core.Services;
using IosPhotoImporter.Infrastructure.Data;
using IosPhotoImporter.Infrastructure.Services;
using IosPhotoImporter.Infrastructure.Tests.TestDoubles;
using IosPhotoImporter.Infrastructure.Wpd;

namespace IosPhotoImporter.Infrastructure.Tests.Services;

public sealed class ImportIntegrationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "IosPhotoImporter.Integration.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StartImportAsync_SecondRunImportsOnlyNewItems()
    {
        Directory.CreateDirectory(_tempRoot);

        var dbPath = Path.Combine(_tempRoot, "state.db");
        var destination = Path.Combine(_tempRoot, "imports");

        var transport = new FakeWpdTransport();
        transport.Devices.Add(new WpdDeviceSnapshot("device-1", "iPhone", true, true));
        transport.MediaObjects.Add(new WpdMediaObject("obj-1", "pid-1", "IMG_0001.HEIC", ".HEIC", 100, DateTimeOffset.UtcNow, MediaKind.Image, false));
        transport.MediaObjects.Add(new WpdMediaObject("obj-2", null, "VID_0001.MOV", ".MOV", 200, DateTimeOffset.UtcNow, MediaKind.Video, false));
        transport.SetContent("obj-1", Encoding.UTF8.GetBytes("img-bytes"));
        transport.SetContent("obj-2", Encoding.UTF8.GetBytes("video-bytes"));

        var repository = new SqliteImportStateRepository(new SqliteRepositoryOptions(dbPath));
        var importService = new ImportService(
            repository,
            new WpdDeviceService(transport),
            new WpdMediaDiscoveryService(transport),
            new WpdMediaContentService(transport),
            new SkipIncomingCollisionPolicy());

        var firstJob = await importService.StartImportAsync(new ImportRequest("device-1", destination), CancellationToken.None);
        var firstResult = await WaitForResultAsync(importService, firstJob);

        Assert.NotNull(firstResult);
        Assert.Equal(2, firstResult.ImportedCount);

        var secondJob = await importService.StartImportAsync(new ImportRequest("device-1", destination), CancellationToken.None);
        var secondResult = await WaitForResultAsync(importService, secondJob);

        Assert.NotNull(secondResult);
        Assert.Equal(0, secondResult.ImportedCount);
        Assert.Equal(2, secondResult.SkippedCount);
    }

    private static async Task<ImportResult?> WaitForResultAsync(ImportService service, ImportJobId jobId)
    {
        var max = TimeSpan.FromSeconds(10);
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < max)
        {
            var result = await service.GetResultAsync(jobId, CancellationToken.None);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Import did not finish in time.");
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
