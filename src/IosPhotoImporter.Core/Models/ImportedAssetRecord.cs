namespace IosPhotoImporter.Core.Models;

public sealed record ImportedAssetRecord(
    string DeviceId,
    string? PersistentId,
    string SourceObjectId,
    string FileName,
    long SizeBytes,
    string? Sha256,
    string LocalPath,
    DateTimeOffset ImportedAtUtc);
