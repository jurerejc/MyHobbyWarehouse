using System.Text.Json;

namespace MyHobbyWarehouse.Services;

public class AppSettings
{
    public string DatabasePath { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
}

public static class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyHobbyWarehouse");

    private static readonly string SettingsFile =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly string OldSettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EagleManager");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? Default();
            }
        }
        catch { /* ignore */ }

        // Migration from old EagleManager path
        try
        {
            string oldSettingsFile = Path.Combine(OldSettingsDir, "settings.json");
            if (File.Exists(oldSettingsFile))
            {
                var json = File.ReadAllText(oldSettingsFile);
                var oldSettings = JsonSerializer.Deserialize<AppSettings>(json);
                if (oldSettings != null && !string.IsNullOrEmpty(oldSettings.DatabasePath))
                {
                    var settings = new AppSettings { DatabasePath = oldSettings.DatabasePath };
                    Save(settings);
                    return settings;
                }
            }
        }
        catch { /* ignore */ }

        return Default();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }

    public static string DefaultDbPath =>
        Path.Combine(SettingsDir, "myhobbywarehouse.db");

    private static AppSettings Default() => new() { DatabasePath = DefaultDbPath };
}
