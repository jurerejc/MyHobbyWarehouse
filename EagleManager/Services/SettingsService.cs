using System.Text.Json;

namespace EagleManager.Services;

public class AppSettings
{
    public string DatabasePath { get; set; } = string.Empty;
}

public static class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EagleManager");

    private static readonly string SettingsFile =
        Path.Combine(SettingsDir, "settings.json");

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
        catch { /* ignore, return default */ }
        return Default();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }

    public static string DefaultDbPath =>
        Path.Combine(SettingsDir, "eagle_manager.db");

    private static AppSettings Default() => new() { DatabasePath = DefaultDbPath };
}
