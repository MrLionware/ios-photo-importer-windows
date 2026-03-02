namespace IosPhotoImporter.App.Settings;

public interface IAppPreferencesStore
{
    AppPreferences Load();

    void Save(AppPreferences preferences);
}
