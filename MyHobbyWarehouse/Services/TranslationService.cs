using System.Globalization;
using System.Text.Json;

namespace MyHobbyWarehouse.Services;

public static class TranslationService
{
    private static Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

    public static string CurrentLanguage { get; private set; } = "en";

    public static string UserLanguagesDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "MyHobbyWarehouse", "Languages");

    public static void Load(string languageCode)
    {
        CurrentLanguage = languageCode;
        _strings.Clear();

        try
        {
            string? file = FindFile(languageCode) ?? FindFile("en");
            if (file == null) return;

            var json = File.ReadAllText(file);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null)
                _strings = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
        }
        catch { /* fallback to empty */ }
    }

    public static List<(string Code, string Name)> GetAvailableLanguages()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string Code, string Name)>();

        void ScanDir(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, "strings.*.json"))
            {
                string code = Path.GetFileNameWithoutExtension(file).Replace("strings.", "");
                if (string.IsNullOrEmpty(code) || !seen.Add(code)) continue;

                string name = GetLanguageNameFromFile(file, code);
                result.Add((code, name));
            }
        }

        // Built-in languages first, then user-imported (user overrides win)
        ScanDir(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"));
        ScanDir(UserLanguagesDir);

        if (result.All(r => r.Code != "en"))
            result.Insert(0, ("en", "English"));

        return result;
    }

    public static string GetLanguageName(string code)
    {
        string? file = FindFile(code);
        if (file != null)
            return GetLanguageNameFromFile(file, code);
        try { return CultureInfo.GetCultureInfo(code).NativeName; }
        catch { return code; }
    }

    public static void ImportLanguage(string sourceFilePath)
    {
        string fileName = Path.GetFileName(sourceFilePath);
        if (!fileName.StartsWith("strings.", StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Filename must be strings.{code}.json");

        Directory.CreateDirectory(UserLanguagesDir);
        string dest = Path.Combine(UserLanguagesDir, fileName);
        File.Copy(sourceFilePath, dest, overwrite: true);
    }

    public static string CreateLanguage(string name, string code)
    {
        string? src = FindFile("en");
        if (src == null) throw new InvalidOperationException("English template not found.");

        string json = File.ReadAllText(src);
        // Update or add _name key
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        dict["_name"] = name;
        json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });

        Directory.CreateDirectory(UserLanguagesDir);
        string dest = Path.Combine(UserLanguagesDir, $"strings.{code}.json");
        File.WriteAllText(dest, json);
        return dest;
    }

    public static void DeleteLanguage(string code)
    {
        string userFile = Path.Combine(UserLanguagesDir, $"strings.{code}.json");
        if (File.Exists(userFile)) File.Delete(userFile);
    }

    public static bool IsUserLanguage(string code)
    {
        string userFile = Path.Combine(UserLanguagesDir, $"strings.{code}.json");
        return File.Exists(userFile);
    }

    public static Dictionary<string, string> LoadAllKeys(string code)
    {
        string? file = FindFile(code);
        if (file == null) return new();
        try
        {
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch { return new(); }
    }

    public static void SaveAllKeys(string code, Dictionary<string, string> keys)
    {
        // Save to user directory so we don't overwrite built-in files
        string dest = Path.Combine(UserLanguagesDir, $"strings.{code}.json");
        Directory.CreateDirectory(UserLanguagesDir);
        var json = JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dest, json);
    }

    public static string GetLanguageFilePath(string code)
    {
        return FindFile(code) ?? Path.Combine(UserLanguagesDir, $"strings.{code}.json");
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static string? FindFile(string languageCode)
    {
        string builtIn = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", $"strings.{languageCode}.json");
        if (File.Exists(builtIn)) return builtIn;

        string user = Path.Combine(UserLanguagesDir, $"strings.{languageCode}.json");
        if (File.Exists(user)) return user;

        return null;
    }

    private static string GetLanguageNameFromFile(string file, string fallbackCode)
    {
        try
        {
            var json = File.ReadAllText(file);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict?.TryGetValue("_name", out var n) == true && !string.IsNullOrEmpty(n))
                return n;
        }
        catch { }

        try { return CultureInfo.GetCultureInfo(fallbackCode).NativeName; }
        catch { return fallbackCode; }
    }

    public static string Get(string key, params object?[] args)
    {
        if (_strings.TryGetValue(key, out var val))
            return args.Length > 0 ? string.Format(val, args) : val;
        return key;
    }

    public static string Get(string key) => _strings.TryGetValue(key, out var val) ? val : key;
}
