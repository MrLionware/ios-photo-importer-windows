using IosPhotoImporter.App.ViewModels;
using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IosPhotoImporter.App.Pages;

public sealed partial class DevicePage : Page
{
    private readonly IDeviceService _deviceService;
    private readonly ImportWorkflowState _workflowState;

    public DevicePage()
    {
        InitializeComponent();
        _deviceService = App.Host.Services.GetRequiredService<IDeviceService>();
        _workflowState = App.Host.Services.GetRequiredService<ImportWorkflowState>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshDevicesAsync();
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await RefreshDevicesAsync();
    }

    private void OnContinueClicked(object sender, RoutedEventArgs e)
    {
        if (DevicesList.SelectedItem is not DeviceInfo selected)
        {
            return;
        }

        _workflowState.SelectedDeviceId = selected.DeviceId;
        _workflowState.SelectedDeviceName = selected.Name;
        Frame.Navigate(typeof(SetupPage));
    }

    private async Task RefreshDevicesAsync()
    {
        var devices = await _deviceService.GetConnectedDevicesAsync();
        DevicesList.ItemsSource = devices;
        if (devices.Count > 0)
        {
            DevicesList.SelectedIndex = 0;
        }
    }
}
