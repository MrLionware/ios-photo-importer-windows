using System.Diagnostics;
using System.Text;
using IosPhotoImporter.App.ViewModels;
using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IosPhotoImporter.App.Pages;

public sealed partial class SummaryPage : Page
{
    private readonly ImportWorkflowState _workflowState;
    private readonly IImportStateRepository _repository;

    public SummaryPage()
    {
        InitializeComponent();
        _workflowState = App.Host.Services.GetRequiredService<ImportWorkflowState>();
        _repository = App.Host.Services.GetRequiredService<IImportStateRepository>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var result = _workflowState.LastResult;
        if (result is null)
        {
            SummaryText.Text = "No completed import available yet.";
            DurationText.Text = string.Empty;
            DetailsText.Text = string.Empty;
            return;
        }

        SummaryText.Text = $"Imported: {result.ImportedCount}, Skipped: {result.SkippedCount}, Failed: {result.FailedCount}, Bytes: {result.TotalBytesTransferred:N0}";
        DurationText.Text = $"Duration: {result.Duration}";

        if (_workflowState.CurrentJobId is null)
        {
            DetailsText.Text = "No item-level details are available for this run.";
            return;
        }

        try
        {
            await _repository.InitializeAsync(CancellationToken.None);
            var items = await _repository.GetJobItemsAsync(_workflowState.CurrentJobId.Value, CancellationToken.None);
            var details = BuildDetailsText(items);
            var job = await _repository.GetJobAsync(_workflowState.CurrentJobId.Value, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(job?.LastError))
            {
                details = $"{details}{Environment.NewLine}{Environment.NewLine}Job Error{Environment.NewLine}- {job.LastError}";
            }

            DetailsText.Text = details;
        }
        catch (Exception ex)
        {
            DetailsText.Text = $"Could not load item details: {ex.Message}";
        }
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

    private static string BuildDetailsText(IReadOnlyList<ImportJobItem> items)
    {
        var skipped = items
            .Where(x => x.State == ImportItemState.Skipped)
            .GroupBy(FormatReason)
            .Select(x => (Reason: x.Key, Count: x.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var failed = items
            .Where(x => x.State is ImportItemState.Failed or ImportItemState.FailedTemporary)
            .GroupBy(FormatReason)
            .Select(x => (Reason: x.Key, Count: x.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        if (skipped.Count == 0 && failed.Count == 0)
        {
            return "No skipped or failed items in this run.";
        }

        var builder = new StringBuilder();
        if (skipped.Count > 0)
        {
            builder.AppendLine("Skipped");
            foreach (var entry in skipped)
            {
                builder.AppendLine($"- {entry.Count} x {entry.Reason}");
            }
            builder.AppendLine();
        }

        if (failed.Count > 0)
        {
            builder.AppendLine("Failed");
            foreach (var entry in failed)
            {
                builder.AppendLine($"- {entry.Count} x {entry.Reason}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatReason(ImportJobItem item)
    {
        var code = string.IsNullOrWhiteSpace(item.ErrorCode) ? null : item.ErrorCode;
        var message = string.IsNullOrWhiteSpace(item.ErrorMessage) ? "No details available." : item.ErrorMessage;

        return code is null
            ? message
            : $"{code}: {message}";
    }
}
