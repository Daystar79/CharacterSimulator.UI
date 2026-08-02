using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.Services;

public record DetectedLlmEngine(string Id, string DisplayName, bool IsAvailable, string StatusDetail);

public class LlmEngineDetector
{
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

    /// <summary>
    /// Auto-detects locally available LLM providers (CLI tools & local server APIs).
    /// </summary>
    public static async Task<List<DetectedLlmEngine>> DetectAvailableEnginesAsync()
    {
        var engines = new List<DetectedLlmEngine>();

        // 1. Check Mistral Vibe CLI
        bool mistralFound = IsCommandAvailable("vibe") || IsCommandAvailable("mistral-vibe");
        engines.Add(new DetectedLlmEngine(
            "MistralVibe",
            "⚡ Mistral Vibe (Local CLI)",
            mistralFound,
            mistralFound ? "CLI detected in system PATH" : "CLI not found in PATH"
        ));

        // 2. Check Ollama Local API
        bool ollamaRunning = false;
        string ollamaStatus = "Server offline at http://localhost:11434";
        try
        {
            var res = await _httpClient.GetAsync("http://localhost:11434/api/tags");
            if (res.IsSuccessStatusCode)
            {
                ollamaRunning = true;
                ollamaStatus = "Active at http://localhost:11434";
            }
        }
        catch
        {
            ollamaStatus = "Connection refused at http://localhost:11434";
        }

        engines.Add(new DetectedLlmEngine(
            "Ollama",
            "🦙 Ollama API",
            ollamaRunning,
            ollamaStatus
        ));

        // 3. Fallback Mock Engine (Always Available)
        engines.Add(new DetectedLlmEngine(
            "MockEngine",
            "🧪 Mock LLM Engine",
            true,
            "Always ready (Offline testing)"
        ));

        return engines;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            proc.WaitForExit();
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
