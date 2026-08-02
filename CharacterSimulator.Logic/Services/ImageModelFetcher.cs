using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.Services;

/// <summary>
/// Image / checkpoint "model" options for a given image engine (not vision-only LLMs).
/// </summary>
public record ImageModelOption(string Id, string DisplayName, string EngineId, string Description = "", bool IsDefault = false);

/// <summary>
/// Fetches or lists models for imaging backends. Pollinations always has static options;
/// SD WebUI is probed live; agent engines use a single "agent-default" entry.
/// </summary>
public static class ImageModelFetcher
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, (DateTime Utc, List<ImageModelOption> Models)> Cache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public static void ClearCache(string? engineId = null)
    {
        lock (CacheLock)
        {
            if (string.IsNullOrWhiteSpace(engineId))
                Cache.Clear();
            else
                Cache.Remove(engineId.Trim());
        }
    }

    public static async Task<List<ImageModelOption>> GetModelsForEngineAsync(
        string engineId,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        string key = string.IsNullOrWhiteSpace(engineId)
            ? ImageEngineDetector.DefaultEngineId
            : engineId.Trim();

        if (!forceRefresh)
        {
            lock (CacheLock)
            {
                if (Cache.TryGetValue(key, out var hit) &&
                    DateTime.UtcNow - hit.Utc < CacheDuration &&
                    hit.Models.Count > 0)
                {
                    return hit.Models.ToList();
                }
            }
        }

        List<ImageModelOption> models = key switch
        {
            "PollinationsAI" => GetPollinationsModels(),
            "StableDiffusionWebUI" => await FetchSdWebUiModelsAsync(ct).ConfigureAwait(false),
            "AgentAgy" => new List<ImageModelOption>
            {
                new("agent-default", "AGY default image path", "AgentAgy", "CLI emit (probe-passed)", true)
            },
            "AgentGrok" => new List<ImageModelOption>
            {
                new("agent-default", "Grok default image path", "AgentGrok", "CLI emit (probe-passed)", true)
            },
            _ => GetPollinationsModels()
        };

        if (models.Count == 0)
            models = GetPollinationsModels();

        lock (CacheLock)
        {
            Cache[key] = (DateTime.UtcNow, models);
        }

        return models.ToList();
    }

    public static List<ImageModelOption> GetPollinationsModels() => new()
    {
        new("flux", "Flux (default quality)", "PollinationsAI", "Balanced quality", true),
        new("turbo", "Turbo (faster)", "PollinationsAI", "Lower latency"),
        new("gptimage", "GPT Image", "PollinationsAI", "Alternate renderer"),
    };

    private static async Task<List<ImageModelOption>> FetchSdWebUiModelsAsync(CancellationToken ct)
    {
        var list = new List<ImageModelOption>();
        try
        {
            using var res = await Http.GetAsync("http://localhost:7860/sdapi/v1/sd-models", ct)
                .ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                return SdFallback();

            string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return SdFallback();

            bool first = true;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                string? title = el.TryGetProperty("title", out var t) ? t.GetString() : null;
                string? modelName = el.TryGetProperty("model_name", out var m) ? m.GetString() : null;
                string id = !string.IsNullOrWhiteSpace(modelName) ? modelName! :
                    (!string.IsNullOrWhiteSpace(title) ? title! : "");
                if (string.IsNullOrWhiteSpace(id)) continue;

                list.Add(new ImageModelOption(
                    id,
                    string.IsNullOrWhiteSpace(title) ? id : title!,
                    "StableDiffusionWebUI",
                    "Local checkpoint",
                    first));
                first = false;
            }

            return list.Count > 0 ? list : SdFallback();
        }
        catch
        {
            return SdFallback();
        }
    }

    private static List<ImageModelOption> SdFallback() => new()
    {
        new("default", "SD default checkpoint", "StableDiffusionWebUI", "Server default", true)
    };

    public static async Task<string> GetDefaultModelIdAsync(string engineId, CancellationToken ct = default)
    {
        var models = await GetModelsForEngineAsync(engineId, forceRefresh: false, ct).ConfigureAwait(false);
        return models.FirstOrDefault(m => m.IsDefault)?.Id
               ?? models.FirstOrDefault()?.Id
               ?? "flux";
    }
}
