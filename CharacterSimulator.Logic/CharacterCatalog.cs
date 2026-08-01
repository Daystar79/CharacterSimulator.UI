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
        var candidates = new List<string>();

        // 1. App executable output directory
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        candidates.Add(Path.Combine(appDir, "Characters"));

        // 2. Working directory
        string current = baseDir ?? Directory.GetCurrentDirectory();
        candidates.Add(Path.Combine(current, "Characters"));

        // 3. Parent directories up to root
        string? parent = Directory.GetParent(current)?.FullName;
        if (parent != null)
        {
            candidates.Add(Path.Combine(parent, "Characters"));
            string? grandParent = Directory.GetParent(parent)?.FullName;
            if (grandParent != null)
            {
                candidates.Add(Path.Combine(grandParent, "Characters"));
            }
        }

        foreach (var dir in candidates)
        {
            if (Directory.Exists(dir) && Directory.GetFiles(dir).Any(IsLoadableCardFile))
                return dir;
        }

        // Fallback: create Characters directory under appDir and seed default cards
        string targetDir = Path.Combine(appDir, "Characters");
        EnsureFallbackCharactersSeeded(targetDir);
        return targetDir;
    }

    private static void EnsureFallbackCharactersSeeded(string charDir)
    {
        try
        {
            if (!Directory.Exists(charDir))
                Directory.CreateDirectory(charDir);

            string defaultCard = Path.Combine(charDir, "serena.md");
            if (!File.Exists(defaultCard))
            {
                File.WriteAllText(defaultCard,
@"---
name: ""Serena""
call_name: ""Serena""
age: 24
canon_adult: true
physical: ""Slender, athletic build with expressive blue eyes and silver-tinted hair.""
active_focus: ""Realm I — Form""
cognitive_bias: ""Self-reliance and quiet observation.""
cognitive_gift: ""Unflappable composure under pressure.""
default_somatic_alignment: ""Calm, steady breathing.""
somatic_zones:
  - ""Face/Eyes: calm gaze""
  - ""Chest/Breath: steady, rhythmic breath""
voice:
  baseline: ""Clear, low, measured tone.""
  syntactical_engine: ""Linear sentences with calm cadence.""
  conversational_stance: ""collaborative""
---
## Background
A calm wanderer who prefers observation before action.
");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[CharacterCatalog] Seed fallback failed: " + ex.Message);
        }
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
