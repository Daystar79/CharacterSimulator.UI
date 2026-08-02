using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.Services;

/// <summary>
/// Result of probing whether an agent/CLI can emit image bytes (not merely vision-in).
/// </summary>
public sealed class ImageEmitProbeResult
{
    public bool Success { get; init; }
    public string Detail { get; init; } = "";
    public byte[]? SampleBytes { get; init; }
    public string MimeType { get; init; } = "image/png";
}

/// <summary>
/// Capability probe: only agents that return real image bytes (or a fetchable image URL)
/// qualify for the Imaging pipeline. Vision-only consumers (e.g. Claude-like) fail closed.
/// </summary>
public static class ImageEmitProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    private static readonly ConcurrentDictionary<string, (DateTime Utc, ImageEmitProbeResult Result)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);

    /// <summary>Grok Build headless flags (must stay non-interactive).</summary>
    public const string GrokHeadlessFlags =
        "--always-approve --output-format plain --permission-mode bypassPermissions --disable-web-search --no-subagents --no-alt-screen";

    /// <summary>Template: flags then -p {0}. Grok Build writes tool images under ~/.grok/sessions/…/images/.</summary>
    public static string GrokHeadlessTemplate => GrokHeadlessFlags + " -p {0}";

    /// <summary>
    /// AGY / Gemini Antigravity CLI: print mode + auto-approve tools.
    /// Images land under ~/.gemini/antigravity-cli/brain/&lt;id&gt;/*.jpg (not data URIs).
    /// </summary>
    public const string AgyHeadlessTemplate =
        "-p {0} --print-timeout 3m --dangerously-skip-permissions";

    /// <summary>Which agent family produced the stdout we are scraping.</summary>
    public enum AgentImageKind
    {
        Unknown = 0,
        Grok = 1,
        Agy = 2
    }

    private const string ProbePrompt =
        "IMAGE EMIT PROBE. Generate a tiny solid cyan square (NOT red) with your image tool.\n" +
        "After the file is saved on disk, reply with ONLY one of:\n" +
        "1) the full absolute path to the .jpg/.png file\n" +
        "2) data:image/png;base64,... or data:image/jpeg;base64,...\n" +
        "3) an https image URL\n" +
        "No markdown. If you cannot generate images, reply exactly: CANNOT_GENERATE_IMAGES";

    /// <summary>
    /// Cached probe. Pass force:true to re-run (e.g. user clicked Auto-Detect).
    /// </summary>
    public static async Task<ImageEmitProbeResult> ProbeCliAgentAsync(
        string cacheKey,
        string executableName,
        string argumentsTemplate,
        TimeSpan? timeout = null,
        bool force = false,
        CancellationToken ct = default)
    {
        if (!force && Cache.TryGetValue(cacheKey, out var hit) &&
            DateTime.UtcNow - hit.Utc < CacheTtl)
        {
            return hit.Result;
        }

        // Image gen + tool use often needs 60–90s
        var result = await RunCliProbeAsync(
                executableName,
                argumentsTemplate,
                timeout ?? TimeSpan.FromSeconds(90),
                ct)
            .ConfigureAwait(false);

        Cache[cacheKey] = (DateTime.UtcNow, result);
        return result;
    }

    public static void ClearCache(string? cacheKey = null)
    {
        if (string.IsNullOrEmpty(cacheKey))
            Cache.Clear();
        else
            Cache.TryRemove(cacheKey, out _);
    }

    /// <summary>
    /// Extract image bytes from agent stdout/stderr:
    /// data URI, https URL, local path, agent session dirs (AGY brain / Grok sessions).
    /// </summary>
    public static async Task<(byte[]? Bytes, string Mime)> TryExtractImageAsync(
        string text,
        CancellationToken ct = default,
        string? workDir = null,
        DateTime? notBeforeUtc = null,
        AgentImageKind agentKind = AgentImageKind.Unknown)
    {
        if (string.IsNullOrWhiteSpace(text) && agentKind == AgentImageKind.Unknown)
            return (null, "image/png");

        text ??= "";
        if (text.Contains("CANNOT_GENERATE_IMAGES", StringComparison.OrdinalIgnoreCase))
            return (null, "image/png");

        DateTime since = notBeforeUtc ?? DateTime.UtcNow.AddMinutes(-5);

        // 1) data:image/...;base64,...
        var dataUri = Regex.Match(
            text,
            @"data:(image/(?:png|jpeg|jpg|webp));base64,([A-Za-z0-9+/=\r\n]+)",
            RegexOptions.IgnoreCase);
        if (dataUri.Success)
        {
            try
            {
                string mime = dataUri.Groups[1].Value.ToLowerInvariant().Replace("jpg", "jpeg");
                if (!mime.StartsWith("image/")) mime = "image/" + mime;
                string b64 = Regex.Replace(dataUri.Groups[2].Value, @"\s+", "");
                // trim trailing non-base64 junk
                int end = b64.Length;
                while (end > 0 && !"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=".Contains(b64[end - 1]))
                    end--;
                b64 = b64[..end];
                byte[] bytes = Convert.FromBase64String(b64);
                if (LooksLikeImage(bytes) && !LooksLikeSolidColorProbe(bytes))
                    return (bytes, mime.Contains("jpeg") ? "image/jpeg" : mime);
            }
            catch { }
        }

        // 2) https URL
        var urlMatch = Regex.Match(
            text,
            @"https?://[^\s""'<>]+\.(?:png|jpe?g|webp)(?:\?[^\s""'<>]*)?",
            RegexOptions.IgnoreCase);
        if (!urlMatch.Success)
            urlMatch = Regex.Match(text, @"https?://[^\s""'<>]+", RegexOptions.IgnoreCase);

        if (urlMatch.Success)
        {
            try
            {
                string url = urlMatch.Value.TrimEnd(')', ']', '.', ',', '"', '\'');
                byte[] bytes = await Http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
                if (LooksLikeImage(bytes) && !LooksLikeSolidColorProbe(bytes))
                    return (bytes, DetectMime(bytes));
            }
            catch { }
        }

        // 3) Local paths printed by agents (absolute Unix/Windows, or images/1.jpg)
        // Prefer non-probe filenames when multiple paths appear in the transcript.
        var pathHits = new List<string>();
        foreach (Match pathMatch in Regex.Matches(
                     text,
                     @"(?m)(?:^|[\s`""'(])((?:[A-Za-z]:)?[^\s""'<>`]+\.(?:png|jpe?g|webp|gif))\b",
                     RegexOptions.IgnoreCase))
        {
            string rel = pathMatch.Groups[1].Value.Trim().Trim('"', '\'', '`', ',', ')', '(');
            if (rel.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
            pathHits.Add(rel);
        }

        foreach (string rel in pathHits.OrderBy(IsProbeArtifactFileName)) // non-probe first (false < true)
        {
            string? resolved = ResolveLocalImagePath(rel, workDir);
            if (resolved == null) continue;
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(resolved, ct).ConfigureAwait(false);
                if (LooksLikeImage(bytes) && !LooksLikeSolidColorProbe(bytes))
                    return (bytes, DetectMime(bytes));
                // Accept probe only if it is the only hit (handled after loop)
            }
            catch { }
        }

        // Accept probe path only if nothing better was available (probe success path)
        foreach (string rel in pathHits)
        {
            if (!IsProbeArtifactFileName(rel)) continue;
            string? resolved = ResolveLocalImagePath(rel, workDir);
            if (resolved == null) continue;
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(resolved, ct).ConfigureAwait(false);
                if (LooksLikeImage(bytes))
                    return (bytes, DetectMime(bytes));
            }
            catch { }
        }

        // 4) Files written into our temp workDir during the run
        var fromWork = TryReadNewestImageUnder(workDir, since, excludeProbeNames: true);
        if (fromWork.Bytes != null)
            return fromWork;

        // 5) Agent-specific on-disk harvest (AGY brain first for AGY, Grok sessions for Grok)
        if (agentKind is AgentImageKind.Agy or AgentImageKind.Unknown)
        {
            var agy = TryReadNewestAgyBrainImage(since, excludeProbeNames: true);
            if (agy.Bytes != null)
                return agy;
        }

        if (agentKind is AgentImageKind.Grok or AgentImageKind.Unknown)
        {
            var grok = TryReadNewestGrokSessionImage(since, excludeProbeNames: true);
            if (grok.Bytes != null)
                return grok;
        }

        // 6) Last resort: allow probe-named files for emit probe validation
        if (agentKind is AgentImageKind.Agy or AgentImageKind.Unknown)
        {
            var agyProbe = TryReadNewestAgyBrainImage(since, excludeProbeNames: false);
            if (agyProbe.Bytes != null)
                return agyProbe;
        }

        if (agentKind is AgentImageKind.Grok or AgentImageKind.Unknown)
        {
            var grokProbe = TryReadNewestGrokSessionImage(since, excludeProbeNames: false);
            if (grokProbe.Bytes != null)
                return grokProbe;
        }

        // 7) Long base64 blob (no data: prefix) — skip solid-color probes
        var b64Only = Regex.Match(text, @"([A-Za-z0-9+/]{200,}={0,2})");
        if (b64Only.Success)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(b64Only.Groups[1].Value);
                if (LooksLikeImage(bytes) && !LooksLikeSolidColorProbe(bytes))
                    return (bytes, DetectMime(bytes));
            }
            catch { }
        }

        return (null, "image/png");
    }

    /// <summary>Resolve relative image path against workDir, cwd, BaseDirectory, Grok sessions, AGY brain.</summary>
    public static string? ResolveLocalImagePath(string pathOrRelative, string? workDir = null)
    {
        if (string.IsNullOrWhiteSpace(pathOrRelative)) return null;
        string p = pathOrRelative.Trim().Trim('"', '\'', '`');

        // Expand ~
        if (p.StartsWith("~/") || p.StartsWith("~\\"))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            p = Path.Combine(home, p[2..].TrimStart('/', '\\'));
        }

        if (File.Exists(p))
            return Path.GetFullPath(p);

        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(workDir))
            candidates.Add(Path.Combine(workDir, p));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), p));
        candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, p));

        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Bare "images/1.jpg" under any recent Grok session
        string sessionsRoot = Path.Combine(homeDir, ".grok", "sessions");
        if (Directory.Exists(sessionsRoot))
        {
            try
            {
                foreach (var sessionDir in Directory.EnumerateDirectories(sessionsRoot)
                             .OrderByDescending(d => Directory.GetLastWriteTimeUtc(d))
                             .Take(12))
                {
                    candidates.Add(Path.Combine(sessionDir, p));
                    candidates.Add(Path.Combine(sessionDir, "images", Path.GetFileName(p)));
                }
            }
            catch { }
        }

        // AGY / Gemini Antigravity: ~/.gemini/antigravity-cli/brain/<uuid>/file.jpg
        string brainRoot = Path.Combine(homeDir, ".gemini", "antigravity-cli", "brain");
        if (Directory.Exists(brainRoot))
        {
            try
            {
                string fileName = Path.GetFileName(p);
                foreach (var brainDir in Directory.EnumerateDirectories(brainRoot)
                             .OrderByDescending(d => Directory.GetLastWriteTimeUtc(d))
                             .Take(24))
                {
                    candidates.Add(Path.Combine(brainDir, p));
                    candidates.Add(Path.Combine(brainDir, fileName));
                }
            }
            catch { }
        }

        foreach (var c in candidates)
        {
            try
            {
                if (File.Exists(c))
                    return Path.GetFullPath(c);
            }
            catch { }
        }

        return null;
    }

    /// <summary>Newest image under ~/.grok/sessions written after notBeforeUtc.</summary>
    public static (byte[]? Bytes, string Mime) TryReadNewestGrokSessionImage(
        DateTime notBeforeUtc,
        bool excludeProbeNames = true)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string sessionsRoot = Path.Combine(home, ".grok", "sessions");
        return TryReadNewestImageUnder(sessionsRoot, notBeforeUtc, excludeProbeNames);
    }

    /// <summary>
    /// Newest image under ~/.gemini/antigravity-cli/brain (AGY/Gemini CLI image tool output).
    /// </summary>
    public static (byte[]? Bytes, string Mime) TryReadNewestAgyBrainImage(
        DateTime notBeforeUtc,
        bool excludeProbeNames = true)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string brainRoot = Path.Combine(home, ".gemini", "antigravity-cli", "brain");
        return TryReadNewestImageUnder(brainRoot, notBeforeUtc, excludeProbeNames);
    }

    /// <summary>Newest image under a directory tree after notBeforeUtc.</summary>
    public static (byte[]? Bytes, string Mime) TryReadNewestImageUnder(
        string? rootDir,
        DateTime notBeforeUtc,
        bool excludeProbeNames = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
                return (null, "image/png");

            string? newest = Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"))
                        return false;
                    if (excludeProbeNames && IsProbeArtifactFileName(f))
                        return false;
                    return true;
                })
                .Select(f => new FileInfo(f))
                .Where(fi => fi.LastWriteTimeUtc >= notBeforeUtc.AddSeconds(-45))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .Select(fi => fi.FullName)
                .FirstOrDefault();

            if (newest == null || !File.Exists(newest))
                return (null, "image/png");

            byte[] bytes = File.ReadAllBytes(newest);
            if (!LooksLikeImage(bytes))
                return (null, "image/png");
            if (excludeProbeNames && LooksLikeSolidColorProbe(bytes))
                return (null, "image/png");
            return (bytes, DetectMime(bytes));
        }
        catch
        {
            return (null, "image/png");
        }
    }

    /// <summary>Probe / emit-test filenames we must not treat as character art.</summary>
    public static bool IsProbeArtifactFileName(string pathOrName)
    {
        if (string.IsNullOrEmpty(pathOrName)) return false;
        string n = Path.GetFileName(pathOrName).ToLowerInvariant();
        return n.Contains("red_square")
               || n.Contains("tiny_red")
               || n.Contains("cyan_square")
               || n.Contains("blue_pixel")
               || n.Contains("pathtest")
               || n.Contains("image_emit")
               || n.Contains("emit_probe")
               || n.StartsWith("probe_");
    }

    /// <summary>
    /// Heuristic: very small solid-color-ish images (emit probes) — skip for portrait harvest.
    /// </summary>
    public static bool LooksLikeSolidColorProbe(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return false;
        // Tiny files under ~8KB are almost never real portraits
        if (bytes.Length < 8_000) return true;
        // Large red_square probes from AGY are often ~300–700KB solid fills — still usable for emit probe.
        // For portrait harvest we rely on filename exclusion; this is only a soft filter.
        return false;
    }

    public static bool LooksLikeImage(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 3) return false;
        // JPEG
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return true;
        // GIF
        if (bytes.Length >= 3 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return true;
        // PNG
        if (bytes.Length >= 4 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return true;
        // WEBP (RIFF....WEBP)
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return true;
        return false;
    }

    public static string DetectMime(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50)
            return "image/png";
        if (bytes.Length >= 12 && bytes[8] == 0x57 && bytes[9] == 0x45)
            return "image/webp";
        if (bytes.Length >= 3 && bytes[0] == 0x47 && bytes[1] == 0x49)
            return "image/gif";
        return "image/png";
    }

    private static async Task<ImageEmitProbeResult> RunCliProbeAsync(
        string executableName,
        string argumentsTemplate,
        TimeSpan timeout,
        CancellationToken ct)
    {
        string? exe = FindOnPath(executableName);
        if (string.IsNullOrEmpty(exe))
        {
            return new ImageEmitProbeResult
            {
                Success = false,
                Detail = $"CLI not found: {executableName}"
            };
        }

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            ApplyArgumentList(proc.StartInfo, argumentsTemplate, ProbePrompt);

            // Expand PATH like CLI clients do for ~/.local/bin
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string extra = Path.Combine(home, ".local", "bin") + Path.PathSeparator +
                           Path.Combine(home, ".agy", "bin");
            proc.StartInfo.Environment["PATH"] = extra + Path.PathSeparator + path;
            // Keep Grok Build / agent CLIs from opening interactive TUIs
            proc.StartInfo.Environment["TERM"] = "dumb";
            proc.StartInfo.Environment["NO_COLOR"] = "1";
            proc.StartInfo.Environment["CI"] = "1";

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            if (!proc.Start())
            {
                return new ImageEmitProbeResult { Success = false, Detail = "Failed to start process" };
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var reg = ct.Register(() => { try { proc.Kill(entireProcessTree: true); } catch { } });
            var waitTask = proc.WaitForExitAsync(ct);
            var delayTask = Task.Delay(timeout, ct);
            var done = await Task.WhenAny(waitTask, delayTask).ConfigureAwait(false);
            if (done != waitTask)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return new ImageEmitProbeResult
                {
                    Success = false,
                    Detail = $"Image emit probe timed out after {timeout.TotalSeconds:0}s"
                };
            }

            string combined = stdout + "\n" + stderr;
            var started = DateTime.UtcNow.AddMinutes(-3);
            var kind = executableName.Contains("agy", StringComparison.OrdinalIgnoreCase)
                       || executableName.Contains("gemini", StringComparison.OrdinalIgnoreCase)
                ? AgentImageKind.Agy
                : executableName.Contains("grok", StringComparison.OrdinalIgnoreCase)
                    ? AgentImageKind.Grok
                    : AgentImageKind.Unknown;
            var (bytes, mime) = await TryExtractImageAsync(
                    combined, ct, workDir: null, notBeforeUtc: started, agentKind: kind)
                .ConfigureAwait(false);
            if (bytes != null && bytes.Length > 0)
            {
                return new ImageEmitProbeResult
                {
                    Success = true,
                    Detail = $"Emitted {mime} ({bytes.Length} bytes)",
                    SampleBytes = bytes,
                    MimeType = mime
                };
            }

            string snip = combined.Trim();
            if (snip.Length > 160) snip = snip[..160] + "…";
            return new ImageEmitProbeResult
            {
                Success = false,
                Detail = string.IsNullOrWhiteSpace(snip)
                    ? "No image bytes or image URL in agent output"
                    : "No image payload in output: " + snip.Replace('\n', ' ')
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ImageEmitProbeResult
            {
                Success = false,
                Detail = "Probe error: " + ex.Message
            };
        }
    }

    /// <summary>
    /// Build argv string (legacy). Prefer <see cref="ApplyArgumentList"/> for ProcessStartInfo.
    /// </summary>
    public static string FormatArgs(string argumentsTemplate, string prompt)
    {
        string quoted = "\"" + prompt.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        if (string.IsNullOrEmpty(argumentsTemplate))
            return quoted;
        if (argumentsTemplate.Contains("{0}"))
            return string.Format(argumentsTemplate, quoted);
        return argumentsTemplate + " " + quoted;
    }

    /// <summary>
    /// Apply template with {0} = prompt as a single argv entry (no shell quoting bugs).
    /// </summary>
    public static void ApplyArgumentList(ProcessStartInfo psi, string argumentsTemplate, string prompt)
    {
        string template = string.IsNullOrWhiteSpace(argumentsTemplate) ? "-p {0}" : argumentsTemplate.Trim();
        template = template.Replace("\"{0}\"", "{0}");
        int idx = template.IndexOf("{0}", StringComparison.Ordinal);
        if (idx < 0)
        {
            foreach (var token in template.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                psi.ArgumentList.Add(token.Trim('"'));
            psi.ArgumentList.Add(prompt);
            return;
        }

        string before = template[..idx];
        string after = template[(idx + 3)..];
        foreach (var token in before.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string t = token.Trim().Trim('"');
            if (t.Length > 0) psi.ArgumentList.Add(t);
        }
        psi.ArgumentList.Add(prompt);
        foreach (var token in after.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string t = token.Trim().Trim('"');
            if (t.Length > 0) psi.ArgumentList.Add(t);
        }
    }

    public static string? FindOnPath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        if (File.Exists(command)) return Path.GetFullPath(command);

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        dirs.Insert(0, Path.Combine(home, ".local", "bin"));
        dirs.Insert(0, Path.Combine(home, ".agy", "bin"));

        foreach (var dir in dirs)
        {
            try
            {
                string candidate = Path.Combine(dir, command);
                if (File.Exists(candidate)) return candidate;
                // Windows
                if (File.Exists(candidate + ".exe")) return candidate + ".exe";
            }
            catch { }
        }

        // which
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(2000);
            if (proc.ExitCode == 0 && File.Exists(output))
                return output;
        }
        catch { }

        return null;
    }
}
