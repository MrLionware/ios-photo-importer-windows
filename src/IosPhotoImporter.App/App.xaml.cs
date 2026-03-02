using IosPhotoImporter.App.ViewModels;
using IosPhotoImporter.App.Logging;
using IosPhotoImporter.Infrastructure.Data;
using IosPhotoImporter.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace IosPhotoImporter.App;

public partial class App : Application
{
    private Window? _window;

    public static IHost Host { get; } = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
        .ConfigureServices((_, services) =>
        {
            var appDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IosPhotoImporter");

            var logDirectory = Path.Combine(appDataDirectory, "logs");
            var dbPath = Path.Combine(
                appDataDirectory,
                "import-state.db");

            services
                .AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddProvider(new LocalFileLoggerProvider(logDirectory));
                    builder.AddDebug();
                })
                .AddSingleton<ImportWorkflowState>()
                .AddIosPhotoImporterInfrastructure(new SqliteRepositoryOptions(dbPath))
                .AddIosPhotoImporterCore()
                .AddSingleton<MainWindow>();
        })
        .Build();

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Host.Start();
        _window = Host.Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }
}
