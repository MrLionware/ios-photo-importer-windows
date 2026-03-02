namespace IosPhotoImporter.Core.Models;

public sealed record ImportJob(
    ImportJobId JobId,
    string DeviceId,
    string DestinationPath,
    ImportJobStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    DateTimeOffset LastCheckpointUtc,
    string? LastError = null);
