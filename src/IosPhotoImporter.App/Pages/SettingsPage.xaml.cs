using IosPhotoImporter.App.ViewModels;
using IosPhotoImporter.App.Settings;
using IosPhotoImporter.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IosPhotoImporter.App.Pages;

public sealed partial class SettingsPage : Page
{
    private const string DefaultLogVerbosity = "Information";

    private readonly ImportWorkflowState _workflowState;
    private readonly IImportStateRepository _repository;
    private readonly IAppPreferencesStore _preferencesStore;

    public SettingsPage()
    {
        InitializeComponent();
        _workflowState = App.Host.Services.GetRequiredService<ImportWorkflowState>();
        _repository = App.Host.Services.GetRequiredService<IImportStateRepository>();
        _preferencesStore = App.Host.Services.GetRequiredService<IAppPreferencesStore>();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var preferences = _preferencesStore.Load();
        DefaultDestinationTextBox.Text = string.IsNullOrWhiteSpace(preferences.DefaultDestinationPath)
            ? _workflowState.DestinationPath
            : preferences.DefaultDestinationPath;
        var verbosity = string.IsNullOrWhiteSpace(preferences.LogVerbosity)
            ? DefaultLogVerbosity
            : preferences.LogVerbosity;

        var matchIndex = LogVerbosityComboBox.Items
            .Select((item, index) => (item, index))
            .FirstOrDefault(x => string.Equals(x.item?.ToString(), verbosity, StringComparison.OrdinalIgnoreCase))
            .index;

        LogVerbosityComboBox.SelectedIndex = matchIndex;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        _workflowState.DestinationPath = DefaultDestinationTextBox.Text;
        var verbosity = LogVerbosityComboBox.SelectedItem?.ToString() ?? DefaultLogVerbosity;

        _preferencesStore.Save(new AppPreferences(
            DefaultDestinationPath: _workflowState.DestinationPath,
            LogVerbosity: verbosity));
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
