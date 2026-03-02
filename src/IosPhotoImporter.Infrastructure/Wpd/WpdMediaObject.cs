using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Infrastructure.Wpd;

public sealed record WpdMediaObject(
    string SourceObjectId,
    string? PersistentId,
    string Name,
    string Extension,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    MediaKind MediaKind,
    bool IsLivePhotoMotionComponent);
