using IosPhotoImporter.App.ViewModels;
using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IosPhotoImporter.App.Pages;

public sealed partial class ProgressPage : Page
{
    private readonly IImportService _importService;
    private readonly ImportWorkflowState _workflowState;
    private readonly DispatcherQueueTimer _pollTimer;

    public ProgressPage()
    {
        InitializeComponent();
        _importService = App.Host.Services.GetRequiredService<IImportService>();
        _workflowState = App.Host.Services.GetRequiredService<ImportWorkflowState>();

        _importService.ProgressChanged += OnProgressChanged;

        _pollTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _pollTimer.Interval = TimeSpan.FromSeconds(1);
        _pollTimer.Tick += OnPollTick;

        Loaded += (_, _) => _pollTimer.Start();
        Unloaded += (_, _) =>
        {
            _pollTimer.Stop();
            _importService.ProgressChanged -= OnProgressChanged;
        };
    }

    private async void OnPollTick(DispatcherQueueTimer sender, object args)
    {
        if (_workflowState.CurrentJobId is null)
        {
            return;
        }

        var result = await _importService.GetResultAsync(_workflowState.CurrentJobId.Value, CancellationToken.None);
        if (result is null)
        {
            return;
        }

        _workflowState.LastResult = result;
        StatusInfoBar.Message = "Import finished.";
        StatusInfoBar.Severity = InfoBarSeverity.Success;
        ProgressBar.IsIndeterminate = false;
    }

    private void OnProgressChanged(object? sender, ImportProgress progress)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _workflowState.LastProgress = progress;
            var currentFile = progress.CurrentFile ?? string.Empty;
            var isScanningPhase = currentFile.StartsWith("Scanning:", StringComparison.OrdinalIgnoreCase);

            if (isScanningPhase)
            {
                var scannedName = currentFile["Scanning:".Length..].Trim();
                CurrentFileText.Text = string.IsNullOrWhiteSpace(scannedName)
                    ? "Preparing import list..."
                    : $"Preparing import list: {scannedName}";
                StatusInfoBar.Message = "Scanning iPhone library...";
                StatusInfoBar.Severity = InfoBarSeverity.Informational;
                ProgressBar.IsIndeterminate = true;
            }
            else
            {
                CurrentFileText.Text = string.IsNullOrWhiteSpace(currentFile)
                    ? "Copying files..."
                    : $"Copying: {currentFile}";
                StatusInfoBar.Message = "Copying files...";
                StatusInfoBar.Severity = InfoBarSeverity.Informational;
                ProgressBar.IsIndeterminate = false;
            }

            CountersText.Text = $"Completed: {progress.Completed}, Skipped: {progress.Skipped}, Failed: {progress.Failed}";
            BytesText.Text = $"Bytes transferred: {progress.BytesTransferred:N0}";

            ProgressBar.Maximum = Math.Max(1, progress.Total);
            ProgressBar.Value = progress.Completed + progress.Skipped + progress.Failed;
        });
    }

    private async void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        if (_workflowState.CurrentJobId is null)
        {
            return;
        }

        await _importService.CancelImportAsync(_workflowState.CurrentJobId.Value);
        StatusInfoBar.Message = "Cancellation requested.";
        StatusInfoBar.Severity = InfoBarSeverity.Warning;
    }

    private async void OnResumeClicked(object sender, RoutedEventArgs e)
    {
        if (_workflowState.CurrentJobId is null)
        {
            return;
        }

        await _importService.ResumeImportAsync(_workflowState.CurrentJobId.Value, CancellationToken.None);
        StatusInfoBar.Message = "Resume requested.";
        StatusInfoBar.Severity = InfoBarSeverity.Informational;
    }

    private void OnSummaryClicked(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SummaryPage));
    }
}
