using System.Windows;
using MyHobbyWarehouse.Data;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = SettingsService.Load();

        TranslationService.Load(settings.Language);

        // First run or DB path missing → ask user
        if (string.IsNullOrEmpty(settings.DatabasePath) || !DbPathIsValid(settings.DatabasePath))
        {
            var dlg = new Views.DbLocationDialog(settings.DatabasePath, settings.Language);
            if (dlg.ShowDialog() != true)
                settings.DatabasePath = SettingsService.DefaultDbPath;
            else
            {
                settings.DatabasePath = dlg.SelectedPath;
                settings.Language = dlg.SelectedLanguage;
            }

            SettingsService.Save(settings);
            TranslationService.Load(settings.Language);
        }

        _ = new DatabaseService(settings.DatabasePath);
        Services.ImageService.Initialize(settings.DatabasePath);
    }

    private static bool DbPathIsValid(string path)
    {
        try { return !string.IsNullOrEmpty(Path.GetDirectoryName(path)); }
        catch { return false; }
    }
}
