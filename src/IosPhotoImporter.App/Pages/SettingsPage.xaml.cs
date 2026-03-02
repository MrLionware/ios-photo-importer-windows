using IosPhotoImporter.App.ViewModels;
using IosPhotoImporter.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace IosPhotoImporter.App.Pages;

public sealed partial class SettingsPage : Page
{
    private const string LogVerbosityKey = "LogVerbosity";

    private readonly ImportWorkflowState _workflowState;
    private readonly IImportStateRepository _repository;

    public SettingsPage()
    {
        InitializeComponent();
        _workflowState = App.Host.Services.GetRequiredService<ImportWorkflowState>();
        _repository = App.Host.Services.GetRequiredService<IImportStateRepository>();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DefaultDestinationTextBox.Text = _workflowState.DestinationPath;
        var localSettings = ApplicationData.Current.LocalSettings;
        var verbosity = localSettings.Values[LogVerbosityKey]?.ToString() ?? "Information";

        var matchIndex = LogVerbosityComboBox.Items
            .Select((item, index) => (item, index))
            .FirstOrDefault(x => string.Equals(x.item?.ToString(), verbosity, StringComparison.OrdinalIgnoreCase))
            .index;

        LogVerbosityComboBox.SelectedIndex = matchIndex;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        _workflowState.DestinationPath = DefaultDestinationTextBox.Text;
        var verbosity = LogVerbosityComboBox.SelectedItem?.ToString() ?? "Information";

        ApplicationData.Current.LocalSettings.Values[LogVerbosityKey] = verbosity;
        ShowStatus("Preferences saved.", InfoBarSeverity.Success);
    }

    private async void OnClearHistoryClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            await _repository.InitializeAsync(CancellationToken.None);
            await _repository.ClearHistoryAsync(CancellationToken.None);
            ShowStatus("Import history cleared.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to clear history: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
