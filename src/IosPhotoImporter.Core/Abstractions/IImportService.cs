using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.Core.Abstractions;

public interface IImportService
{
    event EventHandler<ImportProgress>? ProgressChanged;

    Task<ImportJobId> StartImportAsync(ImportRequest request, CancellationToken ct);

    Task ResumeImportAsync(ImportJobId jobId, CancellationToken ct);

    Task CancelImportAsync(ImportJobId jobId);

    Task<ImportResult?> GetResultAsync(ImportJobId jobId, CancellationToken ct);
}
