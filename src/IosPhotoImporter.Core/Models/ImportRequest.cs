namespace IosPhotoImporter.Core.Models;

public sealed record ImportRequest(
    string DeviceId,
    string DestinationPath,
    ImportMode ImportMode = ImportMode.NewOnly);
