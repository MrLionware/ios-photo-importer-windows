using IosPhotoImporter.Core.Models;
using IosPhotoImporter.Core.Policies;
using IosPhotoImporter.Core.Tests.TestDoubles;

namespace IosPhotoImporter.Core.Tests.Policies;

public sealed class PersistentIdThenHashDuplicatePolicyTests
{
    [Fact]
    public async Task CheckAsync_UsesPersistentIdBeforeHash()
    {
        var repository = new InMemoryImportStateRepository
        {
            PersistentIdExists = true
        };
        var policy = new PersistentIdThenHashDuplicatePolicy(repository);

        var asset = new MediaAsset(
            "obj-1",
            "pid-1",
            "IMG_0001.HEIC",
            ".HEIC",
            10,
            DateTimeOffset.UtcNow,
            MediaKind.Image,
            false);

        var hashFactoryCalled = false;
        var result = await policy.CheckAsync(
            "device-a",
            asset,
            _ =>
            {
                hashFactoryCalled = true;
                return Task.FromResult("ABC");
            },
            CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.False(hashFactoryCalled);
    }

    [Fact]
    public async Task CheckAsync_FallsBackToHashWhenPersistentIdMissing()
    {
        var repository = new InMemoryImportStateRepository
        {
            HashExists = true
        };
        var policy = new PersistentIdThenHashDuplicatePolicy(repository);

        var asset = new MediaAsset(
            "obj-2",
            null,
            "IMG_0002.HEIC",
            ".HEIC",
            20,
            DateTimeOffset.UtcNow,
            MediaKind.Image,
            false);

        var result = await policy.CheckAsync(
            "device-a",
            asset,
            _ => Task.FromResult("HASH123"),
            CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.Equal("Duplicate by hash", result.Reason);
    }

    [Fact]
    public async Task CheckAsync_FallsBackToHashWhenPersistentIdIsPresentButNotKnown()
    {
        var repository = new InMemoryImportStateRepository
        {
            PersistentIdExists = false,
            HashExists = true
        };
        var policy = new PersistentIdThenHashDuplicatePolicy(repository);

        var asset = new MediaAsset(
            "obj-3",
            "pid-3",
            "IMG_0003.HEIC",
            ".HEIC",
            30,
            DateTimeOffset.UtcNow,
            MediaKind.Image,
            false);

        var hashFactoryCalled = false;
        var result = await policy.CheckAsync(
            "device-a",
            asset,
            _ =>
            {
                hashFactoryCalled = true;
                return Task.FromResult("HASH999");
            },
            CancellationToken.None);

        Assert.True(hashFactoryCalled);
        Assert.True(result.IsDuplicate);
        Assert.Equal("Duplicate by hash", result.Reason);
    }
}
