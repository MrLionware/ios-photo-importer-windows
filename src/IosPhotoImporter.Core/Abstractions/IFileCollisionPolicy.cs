using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Abstractions;

public interface IFileCollisionPolicy
{
    FileCollisionAction Resolve(string destinationPath, bool destinationExists);
}
