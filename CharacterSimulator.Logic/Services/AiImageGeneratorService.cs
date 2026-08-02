using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.Services;

public enum ImageGeneratorEngine
{
    /// <summary>Default: free web image API — no keys, always usable.</summary>
    PollinationsAI = 0,
    /// <summary>Local Automatic1111 / Forge SD WebUI.</summary>
    StableDiffusionWebUI = 1,
    /// <summary>AGY / Gemini CLI that passed image-emit probe.</summary>
    AgentAgy = 2,
    /// <summary>Grok CLI that passed image-emit probe.</summary>
    AgentGrok = 3,
    /// <summary>Legacy enum value; treated as Pollinations (vision ≠ generation).</summary>
    OllamaLocal = 4
}

public class PortraitGenerationResult
{
    public string DisplayUri { get; set; } = "";
    public byte[]? ImageBytes { get; set; }
    public string MimeType { get; set; } = "image/jpeg";
}

public class AiImageGeneratorService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    static AiImageGeneratorService()
    {
        Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "CharacterSimulator/1.0 (portrait; desktop)");
    }

    public static Task<string> GeneratePortraitAsync(
        string prompt,
        string characterSlug,
        ImageGeneratorEngine engine = ImageGeneratorEngine.PollinationsAI,
        string? modelId = null,
        bool allowPollinationsFallback = true,
        string? artStyleId = null,
        bool applyArtStyle = true)
    {
        return GeneratePortraitDetailedAsync(
                prompt, characterSlug, engine, default, modelId, allowPollinationsFallback, artStyleId, applyArtStyle)
            .ContinueWith(t => t.Result.DisplayUri, TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <param name="allowPollinationsFallback">
    /// When false, failures from a non-Pollinations engine are returned as empty (no silent redirect).
    /// Generate Art sets this false so the UI can show the real engine failure.
    /// Auto-load portraits may keep true for friend-test resilience.
    /// </param>
    /// <param name="artStyleId">
    /// <see cref="ImageArtStyleCatalog"/> id (anime, photoreal, …). Applied once for all engines.
    /// </param>
    /// <param name="applyArtStyle">
    /// When false, <paramref name="prompt"/> is used as-is (scene builder already embedded style cues).
    /// </param>
    public static async Task<PortraitGenerationResult> GeneratePortraitDetailedAsync(
        string prompt,
        string characterSlug,
        ImageGeneratorEngine engine = ImageGeneratorEngine.PollinationsAI,
        CancellationToken ct = default,
        string? modelId = null,
        bool allowPollinationsFallback = true,
        string? artStyleId = null,
        bool applyArtStyle = true)
    {
        // Style merge once at the gate so Pollinations / SD / agents all see the same look.
        if (applyArtStyle)
        {
            prompt = ImageArtStyleCatalog.ApplyPortraitStyle(
                string.IsNullOrWhiteSpace(prompt)
                    ? $"{characterSlug} character portrait high quality detailed artwork"
                    : prompt,
                artStyleId);
        }
        else if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = $"{characterSlug} environment scene high quality detailed artwork";
        }

        string stem = Path.GetFileNameWithoutExtension(characterSlug);
        if (string.IsNullOrWhiteSpace(stem)) stem = "portrait";
        foreach (var ch in Path.GetInvalidFileNameChars())
            stem = stem.Replace(ch, '_');

        string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Portraits");
        Directory.CreateDirectory(targetDir);
        string localPath = Path.Combine(targetDir, $"{stem}.jpg");
        string model = string.IsNullOrWhiteSpace(modelId) ? "flux" : modelId.Trim();
        string styleLabel = ImageArtStyleCatalog.GetById(artStyleId).DisplayName;

        try
        {
            PortraitGenerationResult result = engine switch
            {
                ImageGeneratorEngine.StableDiffusionWebUI =>
                    await GenerateWithStableDiffusionWebUIAsync(prompt, localPath, model, ct, allowPollinationsFallback)
                        .ConfigureAwait(false),
                ImageGeneratorEngine.AgentAgy =>
                    await GenerateWithCliAgentAsync(
                            "agy",
                            ImageEmitProbe.AgyHeadlessTemplate,
                            prompt,
                            localPath,
                            ct,
                            allowPollinationsFallback,
                            ImageEmitProbe.AgentImageKind.Agy,
                            styleLabel)
                        .ConfigureAwait(false),
                ImageGeneratorEngine.AgentGrok =>
                    await GenerateWithCliAgentAsync(
                            "grok",
                            ImageEmitProbe.GrokHeadlessTemplate,
                            prompt,
                            localPath,
                            ct,
                            allowPollinationsFallback,
                            ImageEmitProbe.AgentImageKind.Grok,
                            styleLabel)
                        .ConfigureAwait(false),
                // OllamaLocal legacy → Pollinations only if that was the resolved engine
                ImageGeneratorEngine.OllamaLocal =>
                    allowPollinationsFallback
                        ? await GenerateWithPollinationsAsync(prompt, localPath, model, ct).ConfigureAwait(false)
                        : EmptyResult(),
                _ => await GenerateWithPollinationsAsync(prompt, localPath, model, ct).ConfigureAwait(false)
            };

            // Optional resilient fallback (auto-portrait path). Explicit Generate Art disables this.
            if (allowPollinationsFallback &&
                (result.ImageBytes == null || result.ImageBytes.Length == 0) &&
                engine is ImageGeneratorEngine.AgentAgy or ImageGeneratorEngine.AgentGrok
                    or ImageGeneratorEngine.StableDiffusionWebUI)
            {
                var fallback = await GenerateWithPollinationsAsync(prompt, localPath, "flux", ct).ConfigureAwait(false);
                if (fallback.ImageBytes is { Length: > 0 })
                    return fallback;
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            if (allowPollinationsFallback || engine == ImageGeneratorEngine.PollinationsAI)
                return await GenerateWithPollinationsAsync(prompt, localPath, model, ct).ConfigureAwait(false);
            return EmptyResult();
        }
    }

    private static PortraitGenerationResult EmptyResult() => new()
    {
        DisplayUri = "",
        ImageBytes = null,
        MimeType = "image/jpeg"
    };

    /// <summary>Legacy alias — prefer <see cref="ImageArtStyleCatalog"/>.</summary>
    public static string DefaultPortraitStyle =>
        ImageArtStyleCatalog.GetById(ImageArtStyleCatalog.DefaultStyleId).PortraitCue;

    private static async Task<PortraitGenerationResult> GenerateWithPollinationsAsync(
        string prompt, string localPath, string modelId, CancellationToken ct)
    {
        // Prompt is expected to already include art-style cues from GeneratePortraitDetailedAsync.
        string cleanPrompt = Uri.EscapeDataString(prompt);
        string modelQ = string.IsNullOrWhiteSpace(modelId) ? "flux" : Uri.EscapeDataString(modelId);
        string imageUrl =
            $"https://image.pollinations.ai/prompt/{cleanPrompt}?width=512&height=512&nologo=true&model={modelQ}&seed={Random.Shared.Next(1, 999999)}";

        try
        {
            byte[] imageBytes = await Http.GetByteArrayAsync(imageUrl, ct).ConfigureAwait(false);
            if (imageBytes is { Length: > 0 } && ImageEmitProbe.LooksLikeImage(imageBytes))
            {
                try { await File.WriteAllBytesAsync(localPath, imageBytes, ct).ConfigureAwait(false); }
                catch { }

                return new PortraitGenerationResult
                {
                    DisplayUri = $"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}",
                    ImageBytes = imageBytes,
                    MimeType = "image/jpeg"
                };
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }

        return new PortraitGenerationResult
        {
            DisplayUri = imageUrl,
            ImageBytes = null,
            MimeType = "image/jpeg"
        };
    }

    private static async Task<PortraitGenerationResult> GenerateWithStableDiffusionWebUIAsync(
        string prompt, string localPath, string checkpoint, CancellationToken ct, bool allowPollinationsFallback = true)
    {
        const string apiUrl = "http://localhost:7860/sdapi/v1/txt2img";
        try
        {
            // Optional override checkpoint via override_settings when not "default"
            object request;
            if (!string.IsNullOrWhiteSpace(checkpoint) &&
                !checkpoint.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                !checkpoint.Equals("agent-default", StringComparison.OrdinalIgnoreCase))
            {
                request = new
                {
                    prompt,
                    negative_prompt = "low quality, blurry, watermark, text",
                    steps = 20,
                    cfg_scale = 7,
                    width = 512,
                    height = 512,
                    sampler_name = "Euler a",
                    batch_size = 1,
                    n_iter = 1,
                    override_settings = new { sd_model_checkpoint = checkpoint }
                };
            }
            else
            {
                request = new
                {
                    prompt,
                    negative_prompt = "low quality, blurry, watermark, text",
                    steps = 20,
                    cfg_scale = 7,
                    width = 512,
                    height = 512,
                    sampler_name = "Euler a",
                    batch_size = 1,
                    n_iter = 1
                };
            }

            using var content = new StringContent(
                JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(apiUrl, content, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return allowPollinationsFallback
                    ? await GenerateWithPollinationsAsync(prompt, localPath, "flux", ct).ConfigureAwait(false)
                    : EmptyResult();

            string responseStr = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(responseStr);
            if (doc.RootElement.TryGetProperty("images", out var images) &&
                images.ValueKind == JsonValueKind.Array)
            {
                foreach (var image in images.EnumerateArray())
                {
                    string? b64 = image.GetString();
                    if (string.IsNullOrEmpty(b64)) continue;

                    // SD often returns raw base64 without data: prefix
                    if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        int comma = b64.IndexOf(',');
                        if (comma > 0) b64 = b64[(comma + 1)..];
                    }

                    byte[] bytes = Convert.FromBase64String(b64);
                    if (!ImageEmitProbe.LooksLikeImage(bytes)) continue;

                    try { await File.WriteAllBytesAsync(localPath, bytes, ct).ConfigureAwait(false); }
                    catch { }

                    return new PortraitGenerationResult
                    {
                        DisplayUri = $"data:image/png;base64,{Convert.ToBase64String(bytes)}",
                        ImageBytes = bytes,
                        MimeType = "image/png"
                    };
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }

        return allowPollinationsFallback
            ? await GenerateWithPollinationsAsync(prompt, localPath, "flux", ct).ConfigureAwait(false)
            : EmptyResult();
    }

    private static async Task<PortraitGenerationResult> GenerateWithCliAgentAsync(
        string executableName,
        string argsTemplate,
        string portraitPrompt,
        string localPath,
        CancellationToken ct,
        bool allowPollinationsFallback = true,
        ImageEmitProbe.AgentImageKind agentKind = ImageEmitProbe.AgentImageKind.Unknown,
        string styleLabel = "Anime")
    {
        string? exe = ImageEmitProbe.FindOnPath(executableName);
        if (string.IsNullOrEmpty(exe))
            return allowPollinationsFallback
                ? await GenerateWithPollinationsAsync(portraitPrompt, localPath, "flux", ct).ConfigureAwait(false)
                : EmptyResult();

        // portraitPrompt is already style-merged at the API gate.
        // AGY saves under ~/.gemini/antigravity-cli/brain/<id>/*.jpg and prints the absolute path.
        // Grok saves under ~/.grok/sessions/.../images/ and may print a relative path.
        string agentPrompt = agentKind switch
        {
            ImageEmitProbe.AgentImageKind.Grok =>
                "Use the image_gen tool to create a character portrait from this description " +
                $"(aspect_ratio 1:1, portrait, art style: {styleLabel}).\n" +
                "When done, reply with ONLY one line: the saved path (e.g. images/1.jpg) " +
                "or a data:image/...;base64,... URI. No other prose. Do NOT draw a solid test square.\n\n" +
                "Description:\n" + portraitPrompt,

            ImageEmitProbe.AgentImageKind.Agy =>
                "Use your image generation tool to create a CHARACTER PORTRAIT (face/upper body) " +
                $"in art style: {styleLabel}. This is NOT a solid-color test square.\n" +
                "Save the image file to disk. When finished, reply with ONLY one line: " +
                "the full absolute path to the saved .jpg or .png file. " +
                "No markdown, no code fences, no extra prose.\n\n" +
                "Description:\n" + portraitPrompt,

            _ =>
                "Generate a single character portrait image from this description " +
                $"(art style: {styleLabel}). " +
                "Reply with ONLY the full absolute path to the saved image file, " +
                "or a data:image/...;base64,... URI, or an https image URL. No other text.\n\n" +
                "Description:\n" + portraitPrompt
        };

        DateTime startedUtc = DateTime.UtcNow;
        try
        {
            string workDir = Path.Combine(Path.GetTempPath(), "cs-img-" + Guid.NewGuid().ToString("N")[..12]);
            Directory.CreateDirectory(workDir);

            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = workDir
                }
            };

            ImageEmitProbe.ApplyArgumentList(proc.StartInfo, argsTemplate, agentPrompt);

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            proc.StartInfo.Environment["PATH"] =
                Path.Combine(home, ".local", "bin") + Path.PathSeparator +
                Path.Combine(home, ".agy", "bin") + Path.PathSeparator + pathEnv;
            proc.StartInfo.Environment["TERM"] = "dumb";
            proc.StartInfo.Environment["NO_COLOR"] = "1";
            proc.StartInfo.Environment["CI"] = "1";

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            if (!proc.Start())
                return allowPollinationsFallback
                    ? await GenerateWithPollinationsAsync(portraitPrompt, localPath, "flux", ct).ConfigureAwait(false)
                    : EmptyResult();

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // AGY + Grok image tool rounds often need 2–4 minutes
            int timeoutMin = agentKind == ImageEmitProbe.AgentImageKind.Grok ? 4 : 3;
            cts.CancelAfter(TimeSpan.FromMinutes(timeoutMin));
            try
            {
                await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                if (ct.IsCancellationRequested) throw;

                // Process killed mid-flight — still try harvest (file may already be on disk)
                var late = await ImageEmitProbe.TryExtractImageAsync(
                        stdout + "\n" + stderr, ct, workDir, startedUtc, agentKind)
                    .ConfigureAwait(false);
                if (late.Bytes is { Length: > 0 })
                    return await ToResult(late.Bytes, late.Mime, localPath, ct).ConfigureAwait(false);

                return allowPollinationsFallback
                    ? await GenerateWithPollinationsAsync(portraitPrompt, localPath, "flux", ct).ConfigureAwait(false)
                    : EmptyResult();
            }

            // Brief settle so filesystem mtime is visible for brain/session harvest
            try { await Task.Delay(400, ct).ConfigureAwait(false); } catch { }

            var (bytes, mime) = await ImageEmitProbe.TryExtractImageAsync(
                    stdout + "\n" + stderr,
                    ct,
                    workDir: workDir,
                    notBeforeUtc: startedUtc,
                    agentKind: agentKind)
                .ConfigureAwait(false);

            if (bytes is { Length: > 0 })
                return await ToResult(bytes, mime, localPath, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch { }

        return allowPollinationsFallback
            ? await GenerateWithPollinationsAsync(portraitPrompt, localPath, "flux", ct).ConfigureAwait(false)
            : EmptyResult();
    }

    private static async Task<PortraitGenerationResult> ToResult(
        byte[] bytes, string mime, string localPath, CancellationToken ct)
    {
        string ext = mime.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        string pathOut = Path.ChangeExtension(localPath, ext);
        try { await File.WriteAllBytesAsync(pathOut, bytes, ct).ConfigureAwait(false); }
        catch { }

        return new PortraitGenerationResult
        {
            DisplayUri = $"data:{mime};base64,{Convert.ToBase64String(bytes)}",
            ImageBytes = bytes,
            MimeType = mime
        };
    }
}
