namespace IosPhotoImporter.Core.Models;

public sealed record ImportProgress(
    ImportJobId JobId,
    int Total,
    int Completed,
    int Skipped,
    int Failed,
    long BytesTransferred,
    string? CurrentFile);
