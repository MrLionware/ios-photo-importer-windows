using System.Text.Json;

namespace IosPhotoImporter.App.Settings;

public sealed class JsonAppPreferencesStore : IAppPreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly object _sync = new();

    public JsonAppPreferencesStore()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IosPhotoImporter",
            "preferences.json");
    }

    public AppPreferences Load()
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return new AppPreferences(DefaultDestinationPath: null);
                }

                var json = File.ReadAllText(_settingsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new AppPreferences(DefaultDestinationPath: null);
                }

                return JsonSerializer.Deserialize<AppPreferences>(json, SerializerOptions)
                    ?? new AppPreferences(DefaultDestinationPath: null);
            }
            catch
            {
                return new AppPreferences(DefaultDestinationPath: null);
            }
        }
    }

    public void Save(AppPreferences preferences)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(preferences, SerializerOptions);
            File.WriteAllText(_settingsPath, json);
        }
    }
}
