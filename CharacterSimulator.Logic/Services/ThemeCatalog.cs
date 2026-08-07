using System;
using System.Collections.Generic;
using System.Linq;

namespace CharacterSimulator.Logic.Services;

public class UiThemePreset
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Primary brand accent (preview / catalog).</summary>
    public string AccentColor { get; set; } = "#38bdf8";

    /// <summary>Deep canvas color for theme preview chrome.</summary>
    public string Surface0 { get; set; } = "#0b1120";

    /// <summary>Panel surface for theme preview body.</summary>
    public string Surface1 { get; set; } = "#0f172a";

    /// <summary>Elevated surface for preview bars.</summary>
    public string Surface2 { get; set; } = "#1e293b";

    /// <summary>Danger/semantic swatch for preview.</summary>
    public string DangerColor { get; set; } = "#f87171";
}

public static class ThemeCatalog
{
    public const string DefaultThemeId = "midnight";

    public static readonly List<UiThemePreset> All = new()
    {
        new UiThemePreset
        {
            Id = "midnight",
            DisplayName = "Midnight Slate",
            Icon = "🌌",
            Description = "Calm product default — deep slate with cyan focus",
            AccentColor = "#38bdf8",
            Surface0 = "#0b1120",
            Surface1 = "#0f172a",
            Surface2 = "#1e293b",
            DangerColor = "#f87171"
        },
        new UiThemePreset
        {
            Id = "cyberpunk",
            DisplayName = "Cyberpunk Synthwave",
            Icon = "🌆",
            Description = "Neon cyan & magenta on purple-black chrome",
            AccentColor = "#00f0ff",
            Surface0 = "#080014",
            Surface1 = "#0d0221",
            Surface2 = "#190a38",
            DangerColor = "#ff4d6d"
        },
        new UiThemePreset
        {
            Id = "matrix",
            DisplayName = "Emerald Matrix",
            Icon = "📟",
            Description = "Terminal emerald on near-black; restrained glow",
            AccentColor = "#10b981",
            Surface0 = "#020a06",
            Surface1 = "#04120b",
            Surface2 = "#0a2416",
            DangerColor = "#fb7185"
        },
        new UiThemePreset
        {
            Id = "amber",
            DisplayName = "Solarized Amber",
            Icon = "🌅",
            Description = "Warm mahogany surfaces with amber focus rings",
            AccentColor = "#f59e0b",
            Surface0 = "#140e0a",
            Surface1 = "#1c130e",
            Surface2 = "#2b1d16",
            DangerColor = "#f43f5e"
        },
        new UiThemePreset
        {
            Id = "obsidian",
            DisplayName = "Obsidian OLED",
            Icon = "🖤",
            Description = "True black canvas with icy silver chrome",
            AccentColor = "#e2e8f0",
            Surface0 = "#000000",
            Surface1 = "#0a0a0a",
            Surface2 = "#141414",
            DangerColor = "#f43f5e"
        }
    };

    public static UiThemePreset GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return All[0];

        return All.FirstOrDefault(t => t.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? All[0];
    }
}
