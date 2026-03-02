namespace IosPhotoImporter.Core.Models;

public enum ImportItemState
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Skipped = 4,
    FailedTemporary = 5,
    Failed = 6
}
