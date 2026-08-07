using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CharacterSimulator.Logic;

/// <summary>
/// Flattens and separates card identity fields so UI/loaders never merge
/// personality, behavior, and physical into one blob.
/// </summary>
public static class CardFieldFormatter
{
    private static readonly string[] PhysicalKeys =
    {
        "summary", "height", "build", "body_details", "hair", "eyes", "skin", "face",
        "distinguishing_features", "defining_features", "posture_movement", "clothing"
    };

    private static readonly string[] StyleKeys =
    {
        "aesthetic", "typical_outfit", "colors", "fabrics_materials", "accessories",
        "footwear", "grooming", "signature_items", "avoid", "clothing"
    };

    public static string FlattenJsonElement(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? "",
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => string.Join("; ",
                value.EnumerateArray()
                    .Select(FlattenJsonElement)
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
            JsonValueKind.Object => string.Join("; ",
                value.EnumerateObject()
                    .Select(p =>
                    {
                        string inner = FlattenJsonElement(p.Value);
                        return string.IsNullOrWhiteSpace(inner) ? "" : $"{p.Name}: {inner}";
                    })
                    .Where(s => s.Length > 0)),
            _ => value.ToString()
        };
    }

    public static string FlattenPhysical(JsonElement root)
    {
        if (!root.TryGetProperty("physical", out var physical))
            return "";

        if (physical.ValueKind == JsonValueKind.String)
            return physical.GetString()?.Trim() ?? "";

        if (physical.ValueKind != JsonValueKind.Object)
            return FlattenJsonElement(physical);

        var parts = new List<string>();
        string? summary = null;
        foreach (var key in PhysicalKeys)
        {
            if (key is "clothing" or "scent") continue; // clothing → style; scent prose-only
            if (!physical.TryGetProperty(key, out var prop)) continue;
            string text = FlattenJsonElement(prop);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (key == "summary")
            {
                summary = text;
                continue;
            }
            parts.Add(text);
        }

        // Any remaining keys except scent/clothing
        foreach (var prop in physical.EnumerateObject())
        {
            if (PhysicalKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase)) continue;
            if (prop.Name.Equals("scent", StringComparison.OrdinalIgnoreCase)) continue;
            if (prop.Name.Equals("clothing", StringComparison.OrdinalIgnoreCase)) continue;
            string text = FlattenJsonElement(prop.Value);
            if (!string.IsNullOrWhiteSpace(text))
                parts.Add(text);
        }

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(summary))
            return summary;
        return string.Join("; ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    public static string FlattenCharacterStyle(JsonElement root)
    {
        if (root.TryGetProperty("character_style", out var style))
        {
            if (style.ValueKind == JsonValueKind.String)
                return style.GetString()?.Trim() ?? "";
            if (style.ValueKind == JsonValueKind.Object)
            {
                var parts = new List<string>();
                foreach (var key in StyleKeys)
                {
                    if (!style.TryGetProperty(key, out var prop)) continue;
                    string text = FlattenJsonElement(prop);
                    if (!string.IsNullOrWhiteSpace(text))
                        parts.Add(text);
                }
                foreach (var prop in style.EnumerateObject())
                {
                    if (StyleKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase)) continue;
                    string text = FlattenJsonElement(prop.Value);
                    if (!string.IsNullOrWhiteSpace(text))
                        parts.Add(text);
                }
                return string.Join("; ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
            return FlattenJsonElement(style);
        }

        // Nested physical.clothing (Shinano-style legacy)
        if (root.TryGetProperty("physical", out var physical) && physical.ValueKind == JsonValueKind.Object)
        {
            if (physical.TryGetProperty("clothing", out var clothing))
                return FlattenJsonElement(clothing);
        }

        return "";
    }

    public static string ReadPersonality(JsonElement root)
    {
        string? direct = GetString(root, "personality");
        if (!string.IsNullOrWhiteSpace(direct))
            return direct.Trim();

        // Lite cards sometimes use vibe as personality under pressure
        string? vibe = GetString(root, "vibe");
        if (!string.IsNullOrWhiteSpace(vibe))
            return vibe.Trim();

        var parts = new List<string>();
        AppendIf(parts, GetString(root, "cultural_bias"));
        AppendIf(parts, GetString(root, "cognitive_bias"));
        AppendIf(parts, GetString(root, "cognitive_gift"));

        if (root.TryGetProperty("psychology", out var psych) && psych.ValueKind == JsonValueKind.Object)
        {
            if (psych.TryGetProperty("temperament", out var temp))
                AppendIf(parts, temp.GetString());
            if (psych.TryGetProperty("core_drives", out var drives) && drives.ValueKind == JsonValueKind.Array)
            {
                string d = string.Join("; ", drives.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(d))
                    parts.Add("Drives: " + d);
            }
        }

        return string.Join("\n", parts);
    }

    public static string ReadBehavior(JsonElement root)
    {
        string? direct = GetString(root, "behavior");
        if (!string.IsNullOrWhiteSpace(direct))
            return direct.Trim();

        var parts = new List<string>();
        AppendIf(parts, GetString(root, "default_somatic_alignment"));

        if (root.TryGetProperty("voice", out var voice) && voice.ValueKind == JsonValueKind.Object)
        {
            if (voice.TryGetProperty("verbal_defense", out var vdef))
            {
                string? s = vdef.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    parts.Add("Under pressure: " + s.Trim());
            }
            if (voice.TryGetProperty("generative_stance", out var gen))
            {
                string? s = gen.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    parts.Add("Under trust: " + s.Trim());
            }
            if (voice.TryGetProperty("signature_tics", out var tics) && tics.ValueKind == JsonValueKind.Array)
            {
                string t = string.Join("; ", tics.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(t))
                    parts.Add("Mannerisms: " + t);
            }
            if (voice.TryGetProperty("conversational_stance", out var stance))
            {
                string? s = stance.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    parts.Add("Social stance: " + s.Trim());
            }
        }

        return string.Join("\n", parts);
    }

    /// <summary>Portrait/image prompt: body + optional default dress. Never personality/behavior.</summary>
    public static string BuildImagingPrompt(string physical, string characterStyle, string? name = null)
    {
        // Prefer structured portrait subject builder (appearance-first wording for image APIs)
        return Services.ImageArtStyleCatalog.BuildPortraitSubject(name, physical, characterStyle);
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    private static void AppendIf(List<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add(value.Trim());
    }
}
