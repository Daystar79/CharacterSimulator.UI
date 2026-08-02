using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace CharacterSimulator.Logic;

/// <summary>
/// Discovers loadable character card files under Characters/.
/// Card files use opaque random IDs as filenames; display names come from card content.
/// </summary>
public static class CharacterCatalog
{
    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "HOW_TO_CARD.md",
        "Relations.md",
        "README.md",
    };

    /// <summary>
    /// A discovered card: stable file identity + human-facing name from the card body.
    /// </summary>
    public record CharacterCardRef(string FileName, string DisplayName, string CardId);

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

    /// <summary>
    /// Generates a new opaque card id (16 lowercase hex chars). Used as the file stem.
    /// </summary>
    public static string GenerateCardId()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Full card filename for a newly allocated id, e.g. "a1b2c3d4e5f60718.json".
    /// </summary>
    public static string GenerateCardFileName() => GenerateCardId() + ".json";

    /// <summary>
    /// Allocates a unique card path under the Characters directory (file is not created).
    /// </summary>
    public static string AllocateCardPath(string? baseDir = null)
    {
        string charDir = ResolveCharactersDirectory(baseDir);
        if (!Directory.Exists(charDir))
            Directory.CreateDirectory(charDir);

        for (int attempt = 0; attempt < 16; attempt++)
        {
            string fileName = GenerateCardFileName();
            string path = Path.Combine(charDir, fileName);
            if (!File.Exists(path))
                return path;
        }

        // Extremely unlikely collision path
        return Path.Combine(charDir, Guid.NewGuid().ToString("N") + ".json");
    }

    public static string GetCardId(string fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath)) return "";
        return Path.GetFileNameWithoutExtension(fileNameOrPath);
    }

    private static void EnsureFallbackCharactersSeeded(string charDir)
    {
        try
        {
            if (!Directory.Exists(charDir))
                Directory.CreateDirectory(charDir);

            // Seed only when the folder has no loadable cards
            if (Directory.GetFiles(charDir).Any(IsLoadableCardFile))
                return;

            string cardId = GenerateCardId();
            string defaultCard = Path.Combine(charDir, cardId + ".json");
            File.WriteAllText(defaultCard,
@"{
  ""name"": ""Serena"",
  ""call_name"": ""Serena"",
  ""age"": 24,
  ""canon_adult"": true,
  ""physical"": ""Slender, athletic build with expressive blue eyes and silver-tinted hair."",
  ""active_focus"": ""Realm I — Form"",
  ""cognitive_bias"": ""Self-reliance and quiet observation."",
  ""cognitive_gift"": ""Unflappable composure under pressure."",
  ""default_somatic_alignment"": ""Calm, steady breathing."",
  ""somatic_zones"": [
    ""Face/Eyes: calm gaze"",
    ""Chest/Breath: steady, rhythmic breath""
  ],
  ""voice"": {
    ""baseline"": ""Clear, low, measured tone."",
    ""syntactical_engine"": ""Linear sentences with calm cadence."",
    ""conversational_stance"": ""collaborative""
  }
}
");
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
        // Catalog is JSON-first; legacy .md cards remain loadable by path but are not listed.
        return ext.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lists cards sorted by display name (from card body). Values are filenames (opaque ids).
    /// </summary>
    public static List<CharacterCardRef> ListCards(string? baseDir = null)
    {
        string charDir = ResolveCharactersDirectory(baseDir);
        if (!Directory.Exists(charDir)) return new List<CharacterCardRef>();

        return Directory.GetFiles(charDir)
            .Where(IsLoadableCardFile)
            .Select(path =>
            {
                string fileName = Path.GetFileName(path);
                string cardId = GetCardId(fileName);
                string displayName = GetCharacterDisplayName(fileName, baseDir);
                return new CharacterCardRef(fileName, displayName, cardId);
            })
            .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Filenames only (opaque card ids + .json), ordered by display name for selector stability.
    /// </summary>
    public static List<string> ListCardFileNames(string? baseDir = null)
    {
        return ListCards(baseDir).Select(c => c.FileName).ToList();
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

    public static string GetCharacterDisplayName(string fileName, string? baseDir = null)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.StartsWith("("))
            return fileName;

        string charDir = ResolveCharactersDirectory(baseDir);
        string filePath = Path.Combine(charDir, fileName);

        if (!File.Exists(filePath))
            return "Unknown Character";

        try
        {
            string content = File.ReadAllText(filePath);
            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                if (root.TryGetProperty("name", out var n))
                {
                    string? name = n.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name.Trim();
                }
                if (root.TryGetProperty("call_name", out var cn))
                {
                    string? callName = cn.GetString();
                    if (!string.IsNullOrWhiteSpace(callName))
                        return callName.Trim();
                }
            }
            else
            {
                // Legacy markdown/YAML cards
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                    {
                        string name = trimmed[5..].Trim(' ', '"', '\'');
                        if (!string.IsNullOrWhiteSpace(name))
                            return name;
                    }
                }
            }
        }
        catch { }

        // Opaque id — never surface the random filename as a "name"
        string id = GetCardId(fileName);
        if (id.Length > 8)
            return $"Unnamed ({id[..8]})";
        return string.IsNullOrEmpty(id) ? "Unnamed Character" : $"Unnamed ({id})";
    }

    public static LoadedCharacterCardInfo LoadCardDetails(string fileName, string? baseDir = null)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.StartsWith("("))
            return new LoadedCharacterCardInfo("(No Character Selected)", 0, "No character card loaded.", "", "", new(), new(), new(), "");

        string charDir = ResolveCharactersDirectory(baseDir);
        string filePath = Path.Combine(charDir, fileName);

        if (!File.Exists(filePath))
            return new LoadedCharacterCardInfo("Unknown Character", 0, "Character card file not found.", "", "", new(), new(), new(), "");

        string content = File.ReadAllText(filePath);
        string name = "";
        int age = 0;
        string description = "";
        string physical = "";
        string cognitiveGift = "";
        var goals = new List<string>();
        var likes = new List<string>();
        var skills = new List<string>();

        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                if (root.TryGetProperty("name", out var n)) name = n.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(name) && root.TryGetProperty("call_name", out var cn))
                    name = cn.GetString() ?? "";
                if (root.TryGetProperty("age", out var a) && a.ValueKind == JsonValueKind.Number)
                    age = a.GetInt32();
                if (root.TryGetProperty("cultural_bias", out var cb)) description = cb.GetString() ?? "";
                if (root.TryGetProperty("physical", out var p)) physical = p.GetString() ?? "";
                if (root.TryGetProperty("cognitive_gift", out var cg)) cognitiveGift = cg.GetString() ?? "";

                if (root.TryGetProperty("somatic_zones", out var sz) && sz.ValueKind == JsonValueKind.Array)
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
                            foreach (var s in genStr.Split(',', ';'))
                            {
                                string trimmed = s.Trim();
                                if (!string.IsNullOrEmpty(trimmed)) skills.Add(trimmed);
                            }
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
            // Markdown parsing (legacy)
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

        if (string.IsNullOrWhiteSpace(name))
            name = GetCharacterDisplayName(fileName, baseDir);

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

        string avatarPath = ResolveAvatarPath(charDir, GetCardId(fileName), name);

        return new LoadedCharacterCardInfo(name, age, description, physical, cognitiveGift, goals, likes, skills, avatarPath);
    }

    private static string ResolveAvatarPath(string charDir, string cardId, string displayName)
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        var stems = new List<string>();
        if (!string.IsNullOrWhiteSpace(cardId)) stems.Add(cardId);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            stems.Add(displayName);
            stems.Add(displayName.ToLowerInvariant());
            stems.Add(displayName.Replace(' ', '_').ToLowerInvariant());
        }

        string[] dirs =
        {
            charDir,
            Path.Combine(appDir, "Assets", "Portraits"),
            Path.Combine(Directory.GetCurrentDirectory(), "CharacterSimulator.GUI", "Assets", "Portraits"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Portraits"),
        };

        string[] exts = { ".png", ".jpg", ".jpeg", ".webp" };

        foreach (var stem in stems.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var dir in dirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                foreach (var ext in exts)
                {
                    string candidate = Path.Combine(dir, stem + ext);
                    if (File.Exists(candidate)) return candidate;
                }
            }
        }

        return "";
    }
}
