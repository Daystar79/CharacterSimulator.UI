using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CharacterSimulator.Logic;

/// <summary>
/// Discovers loadable character card files under Characters/.
/// </summary>
public static class CharacterCatalog
{
    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "HOW_TO_CARD.md",
        "Relations.md",
        "README.md",
    };

    public static string ResolveCharactersDirectory(string? baseDir = null)
    {
        baseDir ??= Directory.GetCurrentDirectory();
        string charDir = Path.Combine(baseDir, "Characters");
        if (Directory.Exists(charDir)) return charDir;

        string? parent = Directory.GetParent(baseDir)?.FullName;
        if (parent != null)
        {
            charDir = Path.Combine(parent, "Characters");
            if (Directory.Exists(charDir)) return charDir;
        }

        return Path.Combine(baseDir, "Characters");
    }

    public static bool IsLoadableCardFile(string pathOrFileName)
    {
        string name = Path.GetFileName(pathOrFileName);
        if (string.IsNullOrEmpty(name) || name.StartsWith('_')) return false;
        if (ExcludedNames.Contains(name)) return false;
        if (name.EndsWith("_state.json", StringComparison.OrdinalIgnoreCase)) return false;
        if (name.EndsWith("_log.yaml", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("_log.yml", StringComparison.OrdinalIgnoreCase)) return false;

        string ext = Path.GetExtension(name);
        return ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> ListCardFileNames(string? baseDir = null)
    {
        string charDir = ResolveCharactersDirectory(baseDir);
        if (!Directory.Exists(charDir)) return new List<string>();

        return Directory.GetFiles(charDir)
            .Where(IsLoadableCardFile)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Cast<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
