namespace IosPhotoImporter.Core.Models;

public enum ImportJobStatus
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Cancelled = 4,
    Failed = 5
}
