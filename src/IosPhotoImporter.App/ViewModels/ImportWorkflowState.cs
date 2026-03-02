using IosPhotoImporter.Core.Models;

namespace IosPhotoImporter.App.ViewModels;

public sealed class ImportWorkflowState
{
    public string? SelectedDeviceId { get; set; }

    public string? SelectedDeviceName { get; set; }

    public string DestinationPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "iOS Imports");

    public ImportJobId? CurrentJobId { get; set; }

    public ImportProgress? LastProgress { get; set; }

    public ImportResult? LastResult { get; set; }
}
