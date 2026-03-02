namespace IosPhotoImporter.App.Settings;

public sealed record AppPreferences(
    string? DefaultDestinationPath,
    string LogVerbosity = "Information");
