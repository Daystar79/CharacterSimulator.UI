using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.Services;

public record DetectedImageEngine(
    string Id,
    string DisplayName,
    bool IsAvailable,
    string StatusDetail,
    ImageGeneratorEngine EngineType);

/// <summary>
/// Discovers backends that can serve the Imaging pipeline (prompt → image bytes).
/// Default is always Pollinations. Multimodal agents are listed only after an emit probe passes.
/// Vision-only models (consume images) are never listed.
/// </summary>
public static class ImageEngineDetector
{
    public const string DefaultEngineId = "PollinationsAI";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    /// <summary>
    /// Auto-detect image generators. Pollinations is always first and is the product default.
    /// Optional agent probes (AGY, Grok) run only when those CLIs are present.
    /// </summary>
    /// <param name="probeAgents">When true, run emit probes for installed multimodal CLIs.</param>
    /// <param name="forceReprobe">Bypass probe cache (e.g. user clicked Auto-Detect).</param>
    public static async Task<List<DetectedImageEngine>> DetectAvailableImageEnginesAsync(
        bool probeAgents = true,
        bool forceReprobe = false,
        CancellationToken ct = default)
    {
        var engines = new List<DetectedImageEngine>
        {
            // 1) Default — always available, no keys
            new(
                DefaultEngineId,
                "✨ Pollinations AI (Default · free web API)",
                true,
                "Default portrait engine — switch only if you need higher quality",
                ImageGeneratorEngine.PollinationsAI)
        };

        // 2) Dedicated local image server
        await AddStableDiffusionIfUpAsync(engines, ct).ConfigureAwait(false);

        // 3) Multimodal agents that can *emit* images (probe, fail closed)
        if (probeAgents)
        {
            await AddAgentIfEmitsAsync(
                engines,
                id: "AgentAgy",
                displayName: "🚀 AGY / Gemini (image emit)",
                executable: "agy",
                // Images land in ~/.gemini/antigravity-cli/brain/ — need skip-permissions + long timeout
                argsTemplate: ImageEmitProbe.AgyHeadlessTemplate,
                engineType: ImageGeneratorEngine.AgentAgy,
                forceReprobe,
                ct).ConfigureAwait(false);

            await AddAgentIfEmitsAsync(
                engines,
                id: "AgentGrok",
                displayName: "🧠 Grok (image emit)",
                executable: "grok",
                // Headless: flags then -p. image_gen saves under ~/.grok/sessions/…/images/
                argsTemplate: ImageEmitProbe.GrokHeadlessTemplate,
                engineType: ImageGeneratorEngine.AgentGrok,
                forceReprobe,
                ct).ConfigureAwait(false);

            // Vibe / Codex / Claude-like: not probed for gen by default — they typically consume, not paint.
            // If product later adds a known image-gen recipe, probe here the same way.
        }

        return engines;
    }

    private static async Task AddStableDiffusionIfUpAsync(List<DetectedImageEngine> engines, CancellationToken ct)
    {
        try
        {
            using var res = await Http.GetAsync("http://localhost:7860/sdapi/v1/sd-models", ct).ConfigureAwait(false);
            if (res.IsSuccessStatusCode)
            {
                engines.Add(new DetectedImageEngine(
                    "StableDiffusionWebUI",
                    "🎨 Stable Diffusion WebUI (Local · higher quality)",
                    true,
                    "Running at http://localhost:7860",
                    ImageGeneratorEngine.StableDiffusionWebUI));
            }
        }
        catch
        {
            // offline — omit from list (user can still type id if needed)
        }
    }

    private static async Task AddAgentIfEmitsAsync(
        List<DetectedImageEngine> engines,
        string id,
        string displayName,
        string executable,
        string argsTemplate,
        ImageGeneratorEngine engineType,
        bool forceReprobe,
        CancellationToken ct)
    {
        if (ImageEmitProbe.FindOnPath(executable) == null)
            return; // not installed — don't show

        var probe = await ImageEmitProbe.ProbeCliAgentAsync(
            cacheKey: id,
            executableName: executable,
            argumentsTemplate: argsTemplate,
            timeout: TimeSpan.FromSeconds(30),
            force: forceReprobe,
            ct: ct).ConfigureAwait(false);

        if (probe.Success)
        {
            engines.Add(new DetectedImageEngine(
                id,
                displayName + " ✅",
                true,
                "Emit probe passed: " + probe.Detail,
                engineType));
        }
        // Fail closed: installed but cannot emit → do not add to Imaging list
        // (still available for Roleplay via LlmEngineDetector)
    }

    public static async Task<List<(string Value, string Label)>> GetImageEngineOptionsAsync(
        bool probeAgents = true,
        bool forceReprobe = false,
        CancellationToken ct = default)
    {
        var engines = await DetectAvailableImageEnginesAsync(probeAgents, forceReprobe, ct).ConfigureAwait(false);
        return engines.ConvertAll(e => (e.Id, e.DisplayName));
    }

    public static ImageGeneratorEngine GetDefaultEngine() => ImageGeneratorEngine.PollinationsAI;

    public static string GetDefaultEngineId() => DefaultEngineId;

    /// <summary>
    /// Resolve settings id to engine enum. Unknown / roleplay-only ids fall back to Pollinations.
    /// </summary>
    public static ImageGeneratorEngine ParseEngineId(string? engineId)
    {
        if (string.IsNullOrWhiteSpace(engineId))
            return ImageGeneratorEngine.PollinationsAI;

        return engineId.Trim() switch
        {
            "StableDiffusionWebUI" => ImageGeneratorEngine.StableDiffusionWebUI,
            "AgentAgy" or "AGY" or "Agy" => ImageGeneratorEngine.AgentAgy,
            "AgentGrok" or "Grok" => ImageGeneratorEngine.AgentGrok,
            // Legacy: Ollama was mis-listed as vision-for-gen — map to default
            "OllamaLocal" or "Ollama" => ImageGeneratorEngine.PollinationsAI,
            "PollinationsAI" => ImageGeneratorEngine.PollinationsAI,
            _ => ImageGeneratorEngine.PollinationsAI
        };
    }

    /// <summary>
    /// If saved preference is not in the available list, fall back to Pollinations.
    /// </summary>
    public static string CoalesceToAvailable(string? preferredId, IEnumerable<DetectedImageEngine> available)
    {
        string pref = string.IsNullOrWhiteSpace(preferredId) ? DefaultEngineId : preferredId.Trim();
        if (available.Any(e => e.IsAvailable && e.Id.Equals(pref, StringComparison.OrdinalIgnoreCase)))
            return pref;
        return DefaultEngineId;
    }
}
