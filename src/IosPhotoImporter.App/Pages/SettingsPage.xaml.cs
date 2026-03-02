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
    private static readonly string DefaultDestinationPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "iOS Imports");
    private static readonly string AppDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IosPhotoImporter");
    private static readonly string DatabasePath = Path.Combine(AppDataDirectory, "import-state.db");
    private static readonly string PreferencesPath = Path.Combine(AppDataDirectory, "preferences.json");
    private static readonly string LogsDirectory = Path.Combine(AppDataDirectory, "logs");

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

    private async void OnResetAppClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            await _repository.InitializeAsync(CancellationToken.None);
            await _repository.ClearHistoryAsync(CancellationToken.None);

            _preferencesStore.Save(new AppPreferences(DefaultDestinationPath: null));
            TryDeleteFile(DatabasePath);
            TryDeleteFile(PreferencesPath);
            TryDeleteDirectory(LogsDirectory);
            CleanupDestinationTempFiles(_workflowState.DestinationPath);

            _workflowState.SelectedDeviceId = null;
            _workflowState.SelectedDeviceName = null;
            _workflowState.CurrentJobId = null;
            _workflowState.LastProgress = null;
            _workflowState.LastResult = null;
            _workflowState.DestinationPath = DefaultDestinationPath;

            DefaultDestinationTextBox.Text = _workflowState.DestinationPath;
            LogVerbosityComboBox.SelectedIndex = 0;
            ShowStatus("App reset complete. You can start a fresh import now.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to reset app: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private static void CleanupDestinationTempFiles(string? destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath) || !Directory.Exists(destinationPath))
        {
            return;
        }

        try
        {
            foreach (var tempPath in Directory.EnumerateFiles(destinationPath, "*.part", SearchOption.TopDirectoryOnly))
            {
                TryDeleteFile(tempPath);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
