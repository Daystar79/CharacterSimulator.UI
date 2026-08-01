using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterSimulator.Logic;

/// <summary>
/// Builds LLM prompts with a hard split:
/// - Identity (who they are) comes only from the character card and never changes with location.
/// - Scene is where/when the exchange happens; it may affect awareness, not personality or voice.
/// </summary>
public static class PromptBuilder
{
    public static string BuildAppearanceSummary(Character character)
    {
        var attrs = character.Attributes;
        if (attrs.TryGetValue("physical", out var physical) && !string.IsNullOrWhiteSpace(physical)
            && !attrs.ContainsKey("hair") && !attrs.ContainsKey("eyes"))
        {
            return physical.Trim();
        }

        var parts = new List<string>();
        foreach (var key in new[] { "height", "build", "hair", "eyes", "skin", "clothing", "defining_features" })
        {
            if (attrs.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                parts.Add(key + ": " + value);
        }

        if (parts.Count == 0 && attrs.TryGetValue("physical", out var fallback) && !string.IsNullOrWhiteSpace(fallback))
            return fallback.Trim();

        return parts.Count > 0 ? string.Join("; ", parts) : "As described in your character identity.";
    }

    public static string BuildIdentityBlock(Character character)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CHARACTER IDENTITY (immutable — this is who you are in every scene):");
        sb.AppendLine("Name: " + character.Name);

        if (!string.IsNullOrWhiteSpace(character.Bio))
            sb.AppendLine("Personality & Background: " + character.Bio.Trim());

        if (!string.IsNullOrWhiteSpace(character.CognitiveBias))
            sb.AppendLine("Cognitive Wound (Defensive Lens): " + character.CognitiveBias.Trim());

        if (!string.IsNullOrWhiteSpace(character.CognitiveGift))
            sb.AppendLine("Cognitive Gift (Generative Lens): " + character.CognitiveGift.Trim());

        if (!string.IsNullOrWhiteSpace(character.CulturalBias))
            sb.AppendLine("Cultural & Background Bias: " + character.CulturalBias.Trim());

        string appearance = BuildAppearanceSummary(character);
        sb.AppendLine("Physical Appearance: " + appearance);

        if (character.Attributes.TryGetValue("voice", out var voice) && !string.IsNullOrWhiteSpace(voice))
            sb.AppendLine("Voice: " + voice.Trim());

        if (!string.IsNullOrWhiteSpace(character.VoiceSyntacticalEngine))
            sb.AppendLine("Syntactical Engine: " + character.VoiceSyntacticalEngine.Trim());

        if (!string.IsNullOrWhiteSpace(character.ConversationalStance))
            sb.AppendLine("Conversational Stance: " + character.ConversationalStance.Trim());

        if (!string.IsNullOrWhiteSpace(character.VerbalDefense))
            sb.AppendLine("Verbal Defense (Defensive Stance): " + character.VerbalDefense.Trim());

        if (!string.IsNullOrWhiteSpace(character.GenerativeStance))
            sb.AppendLine("Generative Stance (Trust Stance): " + character.GenerativeStance.Trim());

        if (character.LatentAnchors.Count > 0)
            sb.AppendLine("Latent Anchors / Subconscious Realms: " + string.Join("; ", character.LatentAnchors));

        if (character.Attributes.TryGetValue("relational_verbal_shifts", out var shifts) && !string.IsNullOrWhiteSpace(shifts))
            sb.AppendLine("Relational Verbal Shifts: " + shifts.Trim());

        if (character.Attributes.TryGetValue("hard_bans", out var bans) && !string.IsNullOrWhiteSpace(bans))
            sb.AppendLine("Never do/say: " + bans.Trim());

        if (character.Attributes.TryGetValue("signature_tics", out var tics) && !string.IsNullOrWhiteSpace(tics))
            sb.AppendLine("Signature mannerisms: " + tics.Trim());

        if (character.ActiveSkills.Count > 0)
            sb.AppendLine("Active Skills & Knowledge Database: " + string.Join("; ", character.ActiveSkills));

        if (character.SomaticZones.Count > 0)
            sb.AppendLine("Baseline somatic vocabulary: " + string.Join("; ", character.SomaticZones));

        return sb.ToString().TrimEnd();
    }

    public static string BuildSceneBlock(string sceneContext)
    {
        if (string.IsNullOrWhiteSpace(sceneContext))
            return "SCENE: (unspecified location — do not invent a genre or world that contradicts your identity.)";

        var sb = new StringBuilder();
        sb.AppendLine("SCENE (location / genre environment only — not your personality):");
        sb.AppendLine(sceneContext.Trim());
        sb.AppendLine("You are physically present in this place. Genre and scenery describe the room, weather, and world texture only.");
        sb.AppendLine("You remain the same person: same history, voice, values, appearance, and mannerisms.");
        sb.AppendLine("Do not become a different archetype because of the setting or genre label.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildSituationBlock(Character character, string input, string goalContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CURRENT SITUATION:");
        sb.AppendLine("Active Focus / State: " + character.CurrentState);
        sb.AppendLine("Active Bias Lens: " + character.BiasState);
        sb.AppendLine("Bond with interlocutor: " + character.Bond);

        if (character.RelationalBaselines.Count > 0)
            sb.AppendLine("Relational Baselines: " + string.Join(", ", character.RelationalBaselines.Select(kv => $"{kv.Key}={kv.Value}")));

        if (character.Memories.Count > 0)
            sb.AppendLine("Durable Memories Database: " + string.Join(" | ", character.Memories));

        if (character.DurableLog?.history != null && character.DurableLog.history.Count > 0)
            sb.AppendLine("Recent Pressure History: " + string.Join(" | ", character.DurableLog.history.TakeLast(3).Select(h => $"[{h.movement}] {h.pressure} ({h.delta})")));

        if (character.SomaticZones.Count > 0)
            sb.AppendLine("Last somatic tells: " + string.Join(", ", character.SomaticZones));

        string realmGuidance = Somatics.RealmDataCatalog.BuildPromptSomaticGuidance(character.ActiveFocus);
        if (!string.IsNullOrWhiteSpace(realmGuidance))
            sb.AppendLine(realmGuidance);

        if (!string.IsNullOrWhiteSpace(goalContext))
            sb.AppendLine(goalContext.Trim());

        if (string.IsNullOrWhiteSpace(input))
            sb.AppendLine("The scene has just opened; no one has spoken to you yet. Take a natural first beat in character.");
        else
            sb.AppendLine("They just said/did: \"" + input.Trim() + "\"");

        return sb.ToString().TrimEnd();
    }

    public static string BuildFullPrompt(Character character, string input, string sceneContext, string goalContext = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are roleplaying as " + character.Name + " and only as " + character.Name + ".");
        sb.AppendLine();
        sb.AppendLine(BuildIdentityBlock(character));
        sb.AppendLine();
        sb.AppendLine(BuildSceneBlock(sceneContext));
        sb.AppendLine();
        sb.AppendLine(BuildSituationBlock(character, input, goalContext));
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("1. Stay strictly in character as defined by CHARACTER IDENTITY. Scene never rewrites who you are.");
        sb.AppendLine("2. Body before insight: Autonomic somatic reactions complete BEFORE labeled cognition or spoken dialogue.");
        sb.AppendLine("3. Dual-Aspect Psyche: Under scene pressure, channel your Cognitive Wound (Defensive Lens). Under trust/safety/flow, channel your Cognitive Gift (Generative Lens).");
        sb.AppendLine("4. Off-page matrix guarantee: NEVER output system terms, raw metrics, or internal scoring inside spoken dialogue. Keep dialogue 100% natural and in-character.");
        sb.AppendLine("5. Somatic reactions must fit YOUR body language vocabulary, not a generic scene archetype.");
        sb.AppendLine();
        sb.AppendLine("Respond in this exact format:");
        sb.AppendLine("[Somatic: {somatic reaction in your style}] {" + character.Name + " dialogue} bond {+1 or -1} [Goal: {status}]");
        return sb.ToString();
    }

    public static string BuildDefaultImagePrompt(Character character, string? sceneContext = null)
    {
        string appearance = BuildAppearanceSummary(character);
        var sb = new StringBuilder();
        sb.Append("Portrait of " + character.Name);
        if (!string.IsNullOrWhiteSpace(appearance) && appearance != "As described in your character identity.")
            sb.Append(", " + appearance);
        sb.Append(", expression: " + character.EmotionEmoji + " " + character.Emotion);
        if (character.SomaticZones.Count > 0)
            sb.Append(", body language: " + string.Join(", ", character.SomaticZones.Take(3)));
        if (!string.IsNullOrWhiteSpace(sceneContext))
            sb.Append(", background setting only: " + sceneContext.Trim());
        sb.Append(". Keep character identity consistent; setting is background only.");
        return sb.ToString();
    }
}
