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
    public string AccentColor { get; set; } = "#38bdf8";
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
            Description = "Deep slate blues with cyan & purple neon highlights (Default)",
            AccentColor = "#38bdf8"
        },
        new UiThemePreset
        {
            Id = "cyberpunk",
            DisplayName = "Cyberpunk Synthwave",
            Icon = "🌆",
            Description = "Dark neon magenta, glowing cyan, and synthwave aesthetics",
            AccentColor = "#ff007f"
        },
        new UiThemePreset
        {
            Id = "matrix",
            DisplayName = "Emerald Matrix",
            Icon = "📟",
            Description = "Deep hacker green terminal theme with glowing emerald accents",
            AccentColor = "#10b981"
        },
        new UiThemePreset
        {
            Id = "amber",
            DisplayName = "Solarized Amber",
            Icon = "🌅",
            Description = "Warm retro amber glow with dark mahogany surfaces",
            AccentColor = "#fbbf24"
        },
        new UiThemePreset
        {
            Id = "obsidian",
            DisplayName = "Obsidian OLED",
            Icon = "🖤",
            Description = "Pure OLED black with icy silver and minimal highlights",
            AccentColor = "#e2e8f0"
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
