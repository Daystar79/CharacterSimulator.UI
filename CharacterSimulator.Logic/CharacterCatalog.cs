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

    public record LoadedCharacterCardInfo(
        string Name,
        int Age,
        string Description,
        string Physical,
        string CognitiveGift,
        List<string> Goals,
        List<string> Likes,
        List<string> Skills,
        string AvatarPath
    );

    public static LoadedCharacterCardInfo LoadCardDetails(string fileName, string? baseDir = null)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.StartsWith("("))
            return new LoadedCharacterCardInfo("(No Character Selected)", 0, "No character card loaded.", "", "", new(), new(), new(), "");

        string charDir = ResolveCharactersDirectory(baseDir);
        string filePath = Path.Combine(charDir, fileName);

        if (!File.Exists(filePath))
            return new LoadedCharacterCardInfo(Path.GetFileNameWithoutExtension(fileName), 0, "Character card file not found.", "", "", new(), new(), new(), "");

        string content = File.ReadAllText(filePath);
        string name = Path.GetFileNameWithoutExtension(fileName);
        if (!string.IsNullOrEmpty(name))
        {
            name = char.ToUpper(name[0]) + name[1..];
        }
        int age = 0;
        string description = "";
        string physical = "";
        string cognitiveGift = "";
        var goals = new List<string>();
        var likes = new List<string>();
        var skills = new List<string>();

        // Try JSON parsing first
        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;
                if (root.TryGetProperty("name", out var n)) name = n.GetString() ?? name;
                if (root.TryGetProperty("age", out var a)) age = a.GetInt32();
                if (root.TryGetProperty("cultural_bias", out var cb)) description = cb.GetString() ?? "";
                if (root.TryGetProperty("physical", out var p)) physical = p.GetString() ?? "";
                if (root.TryGetProperty("cognitive_gift", out var cg)) cognitiveGift = cg.GetString() ?? "";

                if (root.TryGetProperty("somatic_zones", out var sz) && sz.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var elem in sz.EnumerateArray())
                    {
                        var str = elem.GetString();
                        if (!string.IsNullOrEmpty(str)) likes.Add(str.Split(':')[0].Trim());
                    }
                }

                if (root.TryGetProperty("depth_of_knowledge", out var dok))
                {
                    if (dok.TryGetProperty("general", out var gen))
                    {
                        var genStr = gen.GetString();
                        if (!string.IsNullOrEmpty(genStr))
                        {
                            foreach (var s in genStr.Split(',', ';')) skills.Add(s.Trim());
                        }
                    }
                    if (dok.TryGetProperty("personal", out var pers))
                    {
                        var pStr = pers.GetString();
                        if (!string.IsNullOrEmpty(pStr) && string.IsNullOrEmpty(description)) description = pStr;
                    }
                }
            }
            catch { }
        }
        else
        {
            // Markdown parsing
            var lines = content.Split('\n');
            bool inYaml = false;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed == "---")
                {
                    inYaml = !inYaml;
                    continue;
                }

                if (trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                    name = trimmed[5..].Trim(' ', '"', '\'');
                else if (trimmed.StartsWith("age:", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(trimmed[4..].Trim(' ', '"', '\''), out age);
                else if (trimmed.StartsWith("cultural_bias:", StringComparison.OrdinalIgnoreCase))
                    description = trimmed[14..].Trim(' ', '"', '\'');
                else if (trimmed.StartsWith("physical:", StringComparison.OrdinalIgnoreCase))
                    physical = trimmed[9..].Trim(' ', '"', '\'');
                else if (trimmed.StartsWith("cognitive_gift:", StringComparison.OrdinalIgnoreCase))
                    cognitiveGift = trimmed[15..].Trim(' ', '"', '\'');
                else if (trimmed.StartsWith("active_focus:", StringComparison.OrdinalIgnoreCase))
                    goals.Add(trimmed[13..].Trim(' ', '"', '\''));
                else if (trimmed.StartsWith("- \"") || trimmed.StartsWith("- '") || (inYaml && trimmed.StartsWith("- ")))
                {
                    var val = trimmed.TrimStart('-', ' ', '"', '\'').TrimEnd('"', '\'');
                    if (val.Contains(':')) val = val.Split(':')[0].Trim();
                    if (!string.IsNullOrEmpty(val) && likes.Count < 6) likes.Add(val);
                }
            }
        }

        if (string.IsNullOrEmpty(description))
        {
            description = !string.IsNullOrEmpty(physical) ? physical : $"Character card loaded for {name}.";
        }

        if (goals.Count == 0)
        {
            goals.Add("Maintain emotional stability & composure");
            goals.Add("Engage in collaborative dialogue");
        }

        if (likes.Count == 0)
        {
            likes.Add("Quiet Observation");
            likes.Add("Strategic Stance");
            likes.Add("Embodied Presence");
        }

        if (skills.Count == 0)
        {
            skills.Add("Somatic Grounding");
            skills.Add("Tactical Observation");
            skills.Add("Empathy Tuning");
        }

        string avatarPath = Path.Combine(charDir, Path.GetFileNameWithoutExtension(fileName) + ".png");
        if (!File.Exists(avatarPath)) avatarPath = "";

        return new LoadedCharacterCardInfo(name, age, description, physical, cognitiveGift, goals, likes, skills, avatarPath);
    }
}
