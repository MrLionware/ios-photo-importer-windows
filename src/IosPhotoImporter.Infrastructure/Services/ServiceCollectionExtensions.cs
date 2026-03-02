using IosPhotoImporter.Core.Abstractions;
using IosPhotoImporter.Core.Policies;
using IosPhotoImporter.Core.Services;
using IosPhotoImporter.Infrastructure.Data;
using IosPhotoImporter.Infrastructure.Wpd;
using Microsoft.Extensions.DependencyInjection;

namespace IosPhotoImporter.Infrastructure.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIosPhotoImporterCore(this IServiceCollection services)
    {
        services.AddSingleton<IFileCollisionPolicy, SkipIncomingCollisionPolicy>();
        services.AddSingleton<IImportService, ImportService>();
        return services;
    }

    public static IServiceCollection AddIosPhotoImporterInfrastructure(
        this IServiceCollection services,
        SqliteRepositoryOptions sqliteOptions,
        IWpdTransport? transport = null)
    {
        services.AddSingleton(sqliteOptions);
        services.AddSingleton<IImportStateRepository, SqliteImportStateRepository>();

        services.AddSingleton<IWpdTransport>(_ => transport ?? CreateDefaultTransport());
        services.AddSingleton<IDeviceService, WpdDeviceService>();
        services.AddSingleton<IMediaDiscoveryService, WpdMediaDiscoveryService>();
        services.AddSingleton<IMediaContentService, WpdMediaContentService>();
        return services;
    }

    private static IWpdTransport CreateDefaultTransport()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsMediaDeviceTransport();
        }

        return new UnsupportedWpdTransport();
    }
}
