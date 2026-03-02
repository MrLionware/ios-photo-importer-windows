namespace IosPhotoImporter.Core.Models;

public sealed record ImportJobItem(
    ImportJobId JobId,
    string SourceObjectId,
    ImportItemState State,
    string? ErrorCode = null,
    string? ErrorMessage = null);
