namespace IosPhotoImporter.Core.Models;

public sealed record MediaAsset(
    string SourceObjectId,
    string? PersistentId,
    string Name,
    string Extension,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    MediaKind MediaKind,
    bool IsLivePhotoMotionComponent = false);
