using System.Diagnostics;
using IosPhotoImporter.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IosPhotoImporter.App.Pages;

public sealed partial class SummaryPage : Page
{
    private readonly ImportWorkflowState _workflowState;

    public SummaryPage()
    {
        InitializeComponent();
        _workflowState = App.Host.Services.GetRequiredService<ImportWorkflowState>();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var result = _workflowState.LastResult;
        if (result is null)
        {
            SummaryText.Text = "No completed import available yet.";
            DurationText.Text = string.Empty;
            return;
        }

        SummaryText.Text = $"Imported: {result.ImportedCount}, Skipped: {result.SkippedCount}, Failed: {result.FailedCount}, Bytes: {result.TotalBytesTransferred:N0}";
        DurationText.Text = $"Duration: {result.Duration}";
    }

    private void OnOpenFolderClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_workflowState.DestinationPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{_workflowState.DestinationPath}\"",
            UseShellExecute = true
        });
    }

    private void OnViewLogsClicked(object sender, RoutedEventArgs e)
    {
        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IosPhotoImporter",
            "logs");

        Directory.CreateDirectory(logsPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{logsPath}\"",
            UseShellExecute = true
        });
    }

    private void OnRestartClicked(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(DevicePage));
    }
}
