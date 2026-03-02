using IosPhotoImporter.App.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IosPhotoImporter.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootNavigationView.SelectionChanged += OnSelectionChanged;
        Navigate("device");
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag)
        {
            Navigate(tag);
        }
    }

    private void Navigate(string tag)
    {
        var targetPage = tag switch
        {
            "device" => typeof(DevicePage),
            "setup" => typeof(SetupPage),
            "progress" => typeof(ProgressPage),
            "summary" => typeof(SummaryPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(DevicePage)
        };

        if (ContentFrame.CurrentSourcePageType != targetPage)
        {
            ContentFrame.Navigate(targetPage);
        }
    }
}
