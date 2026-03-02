namespace IosPhotoImporter.Core.Models;

public sealed record DeviceHealth(
    DeviceReadinessState State,
    string Message)
{
    public bool IsReady => State == DeviceReadinessState.Ready;
}
