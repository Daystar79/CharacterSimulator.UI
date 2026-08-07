using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.Services;

/// <summary>
/// Host workflow [3] — Derive Canon Card.
/// Fetches documented public canon as SSOT, asks the LLM to map into the card schema
/// under physical/history/knowledge locks, then writes a random-id JSON card file.
/// </summary>
public static class DeriveCardService
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true
    };

    public sealed class AccuracySummary
    {
        public List<string> Sources { get; set; } = new();
        public List<string> Kept { get; set; } = new();
        public List<string> Compressed { get; set; } = new();
        public List<string> LeftBlank { get; set; } = new();
    }

    public sealed class DeriveCardRequest
    {
        public string CharacterName { get; set; } = "";
        /// <summary>Optional filter, e.g. "accurate", "Azur Lane shipgirl", "book version".</summary>
        public string? UserFilter { get; set; }
        /// <summary>User-pasted wiki/official text. When set, skips network fetch.</summary>
        public string? SourcePaste { get; set; }
        /// <summary>LLM provider id/name (e.g. AGY, MistralVibe, MockEngine).</summary>
        public string LlmProvider { get; set; } = "MockEngine";
        public bool SaveToDisk { get; set; } = true;
    }

    public sealed class DeriveCardResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public string CharacterName { get; init; } = "";
        public string? CardFileName { get; init; }
        public string? CardPath { get; init; }
        public string? CardId { get; init; }
        public string? CardJson { get; init; }
        public AccuracySummary Accuracy { get; init; } = new();
        public string SourceLabel { get; init; } = "";
        public string SourceUrl { get; init; } = "";
        public string RawModelOutput { get; init; } = "";
        public bool UsedMockLlm { get; init; }
    }

    public static async Task<DeriveCardResult> DeriveAsync(
        DeriveCardRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return Fail("", "Request is null.");

        string name = (request.CharacterName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Fail("", "Character name is required.");

        // 1) Canon SSOT
        var canon = await CanonSourceFetcher.ResolveAsync(name, request.SourcePaste, ct)
            .ConfigureAwait(false);

        // Allow mock offline derive without network/paste so the feature is testable
        bool allowThinSource = IsMockProvider(request.LlmProvider);
        if (!canon.Success && !allowThinSource)
        {
            return Fail(name, canon.Error ?? "Canon source unavailable.");
        }

        string canonText = canon.Success
            ? canon.Text
            : "(No external canon text available. Produce a minimal card; leave unknown fields as \"unknown\". Do not invent detailed lore.)";

        string sourceLabel = canon.Success ? canon.SourceLabel : "none (thin / mock)";
        string sourceUrl = canon.SourceUrl ?? "";

        // 2) LLM map under locks
        var llm = LlmDiscoveryService.CreateClient(request.LlmProvider);
        string prompt = BuildDerivePrompt(name, request.UserFilter, sourceLabel, sourceUrl, canonText);

        string raw;
        try
        {
            raw = await llm.CompleteRawAsync(prompt, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail(name, $"LLM call failed: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(raw) || raw.Contains("[CLI ERROR", StringComparison.Ordinal))
        {
            return new DeriveCardResult
            {
                Success = false,
                CharacterName = name,
                Error = string.IsNullOrWhiteSpace(raw) ? "LLM returned empty output." : raw.Trim(),
                RawModelOutput = raw ?? "",
                SourceLabel = sourceLabel,
                SourceUrl = sourceUrl,
                UsedMockLlm = IsMockProvider(request.LlmProvider)
            };
        }

        // 3) Parse pack
        if (!TryParseDerivePack(raw, out var cardNode, out var accuracy, out var parseError))
        {
            return new DeriveCardResult
            {
                Success = false,
                CharacterName = name,
                Error = parseError ?? "Could not parse derive pack JSON from model output.",
                RawModelOutput = raw,
                SourceLabel = sourceLabel,
                SourceUrl = sourceUrl,
                UsedMockLlm = IsMockProvider(request.LlmProvider)
            };
        }

        // 4) Enforce hard safety / schema fixes
        EnforceCardInvariants(cardNode, name);

        string cardJson = cardNode.ToJsonString(PrettyJson);
        string displayName = cardNode["name"]?.GetValue<string>() ?? name;

        // 5) Save with random opaque filename
        string? path = null;
        string? fileName = null;
        string? cardId = null;
        if (request.SaveToDisk)
        {
            path = CharacterCatalog.AllocateCardPath();
            fileName = Path.GetFileName(path);
            cardId = CharacterCatalog.GetCardId(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, cardJson, ct).ConfigureAwait(false);

            // SQLite phone book: name/description for selectors without file scan
            try
            {
                CharacterCatalog.UpsertIndexFromFile(
                    path,
                    sourceLabel: string.IsNullOrWhiteSpace(sourceLabel) ? "derived" : sourceLabel,
                    isDerived: true);
            }
            catch
            {
                // Index is optional when ProfileService has not bound SQLite yet (unit tests).
            }
        }

        // Ensure accuracy sources include fetch label
        if (!string.IsNullOrWhiteSpace(sourceLabel) &&
            !accuracy.Sources.Any(s => s.Contains(sourceLabel, StringComparison.OrdinalIgnoreCase)))
        {
            accuracy.Sources.Insert(0, sourceLabel);
        }

        return new DeriveCardResult
        {
            Success = true,
            CharacterName = displayName,
            CardFileName = fileName,
            CardPath = path,
            CardId = cardId,
            CardJson = cardJson,
            Accuracy = accuracy,
            SourceLabel = sourceLabel,
            SourceUrl = sourceUrl,
            RawModelOutput = raw,
            UsedMockLlm = IsMockProvider(request.LlmProvider)
        };
    }

    private static DeriveCardResult Fail(string name, string error) => new()
    {
        Success = false,
        CharacterName = name,
        Error = error
    };

    private static bool IsMockProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ||
        provider.Contains("Mock", StringComparison.OrdinalIgnoreCase);

    internal static string BuildDerivePrompt(
        string characterName,
        string? userFilter,
        string sourceLabel,
        string sourceUrl,
        string canonText)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the CharacterSimulator DERIVE CARD engine.");
        sb.AppendLine("Build an accuracy-locked identity card from DOCUMENTED PUBLIC CANON only.");
        sb.AppendLine();
        sb.AppendLine("HARD CONSTRAINTS:");
        sb.AppendLine("1. Canon SSOT is the SOURCE TEXT below. Model training recall is NOT authority.");
        sb.AppendLine("2. If SOURCE TEXT is thin, leave fields as \"unknown\" — do NOT invent lore, tragedy, or body details.");
        sb.AppendLine("3. Keep these SEPARATE — never merge into one description blob:");
        sb.AppendLine("   - physical: body identity only (height/build/hair/eyes/skin/face/signature features/movement). Forbidden: beautification, body drift, race/species drift, personality adjectives as body, fashion mixed into physical.");
        sb.AppendLine("   - character_style: default dress/accessories/palette only (from documented outfits). Not art medium.");
        sb.AppendLine("   - personality: plain-English who they are (temperament/values/social stance). Not body, not clothes.");
        sb.AppendLine("   - behavior: plain-English how they act under pressure/trust/routine. Not appearance.");
        sb.AppendLine("4. history_anchors: 2–3 coarse scene-useful facts present in source only.");
        sb.AppendLine("5. depth_of_knowledge and hobbies only from what the character demonstrably knows/does in canon.");
        sb.AppendLine("6. Wound (cognitive_bias) & Gift (cognitive_gift) from observed pressure/trust patterns only — engine labels, distinct from personality/behavior prose.");
        sb.AppendLine("7. voice only from how they speak in source. No therapy-speak imports.");
        sb.AppendLine("8. Gaps stay gaps. Missing → \"unknown\" or minimal.");
        sb.AppendLine("9. If age < 18 or unclear adult status: canon_adult must be false; never sexualize.");
        sb.AppendLine("10. User filters may label a variant but must not override canon base facts.");
        sb.AppendLine();
        sb.AppendLine($"TARGET CHARACTER NAME: {characterName}");
        if (!string.IsNullOrWhiteSpace(userFilter))
            sb.AppendLine($"USER FILTER / VARIANT NOTE: {userFilter.Trim()}");
        sb.AppendLine($"SOURCE LABEL: {sourceLabel}");
        if (!string.IsNullOrWhiteSpace(sourceUrl))
            sb.AppendLine($"SOURCE URL: {sourceUrl}");
        sb.AppendLine();
        sb.AppendLine("=== SOURCE TEXT (SSOT) BEGIN ===");
        sb.AppendLine(canonText);
        sb.AppendLine("=== SOURCE TEXT (SSOT) END ===");
        sb.AppendLine();
        sb.AppendLine("OUTPUT FORMAT — respond with ONE JSON object only (no markdown fences, no prose outside JSON):");
        sb.AppendLine("""
            {
              "accuracy_summary": {
                "sources": ["source title or URL"],
                "kept": ["fields kept faithful"],
                "compressed": ["fields shortened not changed"],
                "left_blank": ["fields unknown / thin source"]
              },
              "card": {
                "name": "...",
                "call_name": "...",
                "age": 0,
                "canon_adult": true,
                "physical": "body only: height/build/hair/eyes/skin/face/marks/movement",
                "character_style": "default outfit + accessories + palette",
                "personality": "who they are — temperament/values/social stance",
                "behavior": "how they act under pressure/trust/routine",
                "hobbies": ["...", "..."],
                "voice_archetype": "A-F or hybrid",
                "cultural_bias": "...",
                "active_focus": "Realm N — Name",
                "latent_anchors": ["Realm a — Name", "Realm b — Name"],
                "cognitive_bias": "Bias — rewrite rule",
                "cognitive_gift": "Gift — resonance rule",
                "default_somatic_alignment": "...",
                "somatic_zones": ["Face/Eyes: ...", "Throat/Neck: ...", "Chest/Breath: ...", "Hands/Arms: ...", "Spine/Posture: ...", "Feet/Staging: ..."],
                "transformation_weights": {
                  "active_focus": 70,
                  "latent_anchors": { "II": 15, "VIII": 15 },
                  "bias_strength": 60,
                  "somatic_flexibility": 40
                },
                "depth_of_knowledge": {
                  "general": "...",
                  "esoteric": "...",
                  "personal": "..."
                },
                "voice": {
                  "baseline": "...",
                  "syntactical_engine": "...",
                  "conversational_stance": "dominant|yielding|evasive|counter-querying|directive|buffering|collaborative",
                  "verbal_defense": "...",
                  "generative_stance": "...",
                  "hard_bans": ["..."],
                  "signature_tics": ["..."],
                  "relational_verbal_shifts": {}
                },
                "history_anchors": ["...", "..."],
                "scene_seeds": ["Place + pressure + object"]
              }
            }
            """);
        return sb.ToString();
    }

    /// <summary>
    /// Extract derive pack JSON from raw model output (fences, prose wrapper OK).
    /// </summary>
    public static bool TryParseDerivePack(
        string raw,
        out JsonObject cardNode,
        out AccuracySummary accuracy,
        out string? error)
    {
        cardNode = new JsonObject();
        accuracy = new AccuracySummary();
        error = null;

        string? json = ExtractJsonObject(raw);
        if (json == null)
        {
            error = "No JSON object found in model output.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Shape A: { accuracy_summary, card }
            if (root.TryGetProperty("card", out var cardEl) && cardEl.ValueKind == JsonValueKind.Object)
            {
                cardNode = JsonNode.Parse(cardEl.GetRawText()) as JsonObject
                           ?? new JsonObject();
                if (root.TryGetProperty("accuracy_summary", out var accEl))
                    accuracy = ParseAccuracy(accEl);
                return ValidateCardShape(cardNode, out error);
            }

            // Shape B: bare card object with "name"
            if (root.TryGetProperty("name", out _))
            {
                cardNode = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
                accuracy.LeftBlank.Add("accuracy_summary missing from model output");
                return ValidateCardShape(cardNode, out error);
            }

            error = "JSON found but missing 'card' object and top-level 'name'.";
            return false;
        }
        catch (Exception ex)
        {
            error = "Invalid JSON: " + ex.Message;
            return false;
        }
    }

    private static bool ValidateCardShape(JsonObject card, out string? error)
    {
        error = null;
        if (card["name"] == null || string.IsNullOrWhiteSpace(card["name"]?.ToString()))
        {
            error = "Card JSON missing required 'name' field.";
            return false;
        }
        return true;
    }

    private static AccuracySummary ParseAccuracy(JsonElement el)
    {
        var a = new AccuracySummary();
        if (el.ValueKind != JsonValueKind.Object) return a;
        a.Sources = ReadStringArray(el, "sources");
        a.Kept = ReadStringArray(el, "kept");
        a.Compressed = ReadStringArray(el, "compressed");
        a.LeftBlank = ReadStringArray(el, "left_blank");
        return a;
    }

    private static List<string> ReadStringArray(JsonElement parent, string prop)
    {
        var list = new List<string>();
        if (!parent.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var e in arr.EnumerateArray())
        {
            string? s = e.GetString();
            if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
        }
        return list;
    }

    private static void EnforceCardInvariants(JsonObject card, string fallbackName)
    {
        if (card["name"] == null || string.IsNullOrWhiteSpace(card["name"]?.ToString()))
            card["name"] = fallbackName;

        int age = 25;
        if (card["age"] != null && int.TryParse(card["age"]!.ToString(), out var parsedAge))
            age = parsedAge;
        else
            card["age"] = age;

        // Absolute age gate
        if (age < 18)
            card["canon_adult"] = false;
        else if (card["canon_adult"] == null)
            card["canon_adult"] = true;

        // Ensure call_name
        if (card["call_name"] == null || string.IsNullOrWhiteSpace(card["call_name"]?.ToString()))
            card["call_name"] = card["name"]!.DeepClone();

        // Mark derived provenance without polluting RP fields
        card["derived"] = true;
        if (card["derived_at"] == null)
            card["derived_at"] = DateTime.UtcNow.ToString("o");
    }

    /// <summary>
    /// Pull the first top-level JSON object from model text (handles ```json fences).
    /// </summary>
    public static string? ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string text = raw.Trim();

        // Strip common fences
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNl = text.IndexOf('\n');
            if (firstNl > 0) text = text[(firstNl + 1)..];
            int fence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) text = text[..fence];
            text = text.Trim();
        }

        int start = text.IndexOf('{');
        if (start < 0) return null;

        int depth = 0;
        bool inString = false;
        bool escape = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') inString = false;
                continue;
            }

            if (c == '"') { inString = true; continue; }
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return text[start..(i + 1)];
            }
        }

        return null;
    }
}
