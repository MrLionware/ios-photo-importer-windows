namespace IosPhotoImporter.Core.Models;

public sealed record ImportResult(
    int ImportedCount,
    int SkippedCount,
    int FailedCount,
    TimeSpan Duration,
    long TotalBytesTransferred);
