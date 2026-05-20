using System.Windows;
using EagleManager.Data;
using EagleManager.Services;

namespace EagleManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = SettingsService.Load();

        // First run or DB path missing → ask user
        if (string.IsNullOrEmpty(settings.DatabasePath) || !DbPathIsValid(settings.DatabasePath))
        {
            var dlg = new Views.DbLocationDialog(settings.DatabasePath);
            if (dlg.ShowDialog() != true)
                settings.DatabasePath = SettingsService.DefaultDbPath;
            else
                settings.DatabasePath = dlg.SelectedPath;

            SettingsService.Save(settings);
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
