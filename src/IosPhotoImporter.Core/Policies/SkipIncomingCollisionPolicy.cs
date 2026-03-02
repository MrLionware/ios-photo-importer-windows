using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Policies;

public sealed class SkipIncomingCollisionPolicy : IFileCollisionPolicy
{
    public FileCollisionAction Resolve(string destinationPath, bool destinationExists)
    {
        return destinationExists ? FileCollisionAction.Skip : FileCollisionAction.Write;
    }
}
