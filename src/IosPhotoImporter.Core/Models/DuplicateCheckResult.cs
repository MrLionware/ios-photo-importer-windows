namespace IosPhotoImporter.Core.Models;

public sealed record DuplicateCheckResult(
    bool IsDuplicate,
    string? Reason,
    string? HashHex = null)
{
    public static DuplicateCheckResult NotDuplicate(string? hashHex = null) => new(false, null, hashHex);

    public static DuplicateCheckResult Duplicate(string reason) => new(true, reason);
}
