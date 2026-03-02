using IosPhotoImporter.App.ViewModels;
using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IosPhotoImporter.App.Pages;

public sealed partial class SetupPage : Page
{
    private readonly ImportWorkflowState _workflowState;
    private readonly IImportService _importService;

    public SetupPage()
    {
        InitializeComponent();
        _workflowState = App.Host.Services.GetRequiredService<ImportWorkflowState>();
        _importService = App.Host.Services.GetRequiredService<IImportService>();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SelectedDeviceText.Text = _workflowState.SelectedDeviceName ?? "No device selected";
        DestinationPathTextBox.Text = _workflowState.DestinationPath;
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(DevicePage));
    }

    private async void OnStartClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_workflowState.SelectedDeviceId))
        {
            return;
        }

        _workflowState.DestinationPath = DestinationPathTextBox.Text;

        var request = new ImportRequest(
            _workflowState.SelectedDeviceId,
            _workflowState.DestinationPath,
            ImportMode.NewOnly);

        var jobId = await _importService.StartImportAsync(request, CancellationToken.None);
        _workflowState.CurrentJobId = jobId;
        Frame.Navigate(typeof(ProgressPage));
    }
}
