using IosPhotoImporter.Core.Models;
using IosPhotoImporter.Core.Policies;

namespace IosPhotoImporter.Core.Tests.Policies;

public sealed class SkipIncomingCollisionPolicyTests
{
    [Fact]
    public void Resolve_WhenDestinationExists_ReturnsSkip()
    {
        var policy = new SkipIncomingCollisionPolicy();
        var action = policy.Resolve("C:\\Imports\\IMG_0001.HEIC", destinationExists: true);
        Assert.Equal(FileCollisionAction.Skip, action);
    }

    [Fact]
    public void Resolve_WhenDestinationMissing_ReturnsWrite()
    {
        var policy = new SkipIncomingCollisionPolicy();
        var action = policy.Resolve("C:\\Imports\\IMG_0001.HEIC", destinationExists: false);
        Assert.Equal(FileCollisionAction.Write, action);
    }
}
