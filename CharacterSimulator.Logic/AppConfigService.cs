using System;
using System.IO;
using System.Text.Json;

namespace CharacterSimulator.Logic;

public class AppSettings
{
    public bool IsConfigured { get; set; } = false;
    public string SelectedCharA { get; set; } = "serena.md";
    public string SelectedCharB { get; set; } = "None (Solo Roleplay)";
    public string SelectedLlmA { get; set; } = "Mock";
    public string SelectedLlmB { get; set; } = "Mock";
    public string SelectedGenre { get; set; } = SceneGenreCatalog.DefaultGenreId;
    public string ScenePrompt { get; set; } = SceneGenreCatalog.DefaultSceneFor(SceneGenreCatalog.DefaultGenreId);
    public int MaxTurns { get; set; } = 10;
    public string RoleplayMode { get; set; } = "AutoPlay";
}

public static class AppConfigService
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_config.json");

    public static bool HasConfigFile()
    {
        return File.Exists(ConfigPath);
    }

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                // Normalize genre id if an older display name was stored
                settings.SelectedGenre = SceneGenreCatalog.GetById(settings.SelectedGenre).Id;
                return settings;
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            settings.IsConfigured = true;
            settings.SelectedGenre = SceneGenreCatalog.GetById(settings.SelectedGenre).Id;
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}
