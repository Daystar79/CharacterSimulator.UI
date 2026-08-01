using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic;

public class CliLlmClient : ILLMClient
{
    public string Name { get; }
    public string ExecutablePath { get; }
    /// <summary>
    /// Argument template. Use <c>{0}</c> where the prompt should be inserted as a single argv entry.
    /// Example: <c>-p {0} --auto-approve --output text</c>
    /// </summary>
    public string ArgumentsTemplate { get; }
    public int TimeoutMs { get; set; } = 180_000;

    public CliLlmClient(string name, string executablePath, string argumentsTemplate = "-p {0}")
    {
        Name = name;
        ExecutablePath = executablePath;
        ArgumentsTemplate = argumentsTemplate;
    }

    public string SendPrompt(Character character, string input, string sceneContext, string goalContext = "")
    {
        string prompt = PromptBuilder.BuildFullPrompt(character, input, sceneContext, goalContext);
        return ExecuteCli(prompt);
    }

    private string ExecuteCli(string prompt)
    {
        if (string.IsNullOrWhiteSpace(ExecutablePath) || !File.Exists(ExecutablePath))
        {
            return $"[CLI ERROR: {Name}] Executable not found: '{ExecutablePath}'. " +
                   "Check that it is installed and visible on PATH (GUI apps may miss ~/.local/bin).";
        }

        string tempPromptFile = Path.Combine(Path.GetTempPath(), $"cs_prompt_{Guid.NewGuid():N}.md");

        try
        {
            // Plan B: Write prompt payload to ephemeral temp .md file to prevent command line length limits / corruption
            File.WriteAllText(tempPromptFile, prompt, Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            EnsureExtendedPath(psi);
            ApplyArguments(psi, ArgumentsTemplate, prompt);

            using var process = new Process { StartInfo = psi };
            process.Start();
            try { process.StandardInput.Close(); } catch { }

            // Read stdout + stderr concurrently to avoid classic pipe deadlocks.
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            bool exited = process.WaitForExit(TimeoutMs);
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return $"[CLI ERROR: {Name}] Timed out after {TimeoutMs / 1000}s waiting for '{Path.GetFileName(ExecutablePath)}'. " +
                       "The provider may be waiting for tool approval or network. Try again or use Mock.";
            }

            // Drain readers after exit
            Task.WaitAll(new Task[] { stdoutTask, stderrTask }, 5_000);
            string output = (stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : "").Trim();
            string error = (stderrTask.IsCompletedSuccessfully ? stderrTask.Result : "").Trim();
            int code = process.ExitCode;

            if (!string.IsNullOrWhiteSpace(output))
            {
                // Some CLIs write warnings to stderr but still produce a good answer.
                if (code != 0 && !string.IsNullOrWhiteSpace(error))
                    return output + $"\n[CLI note: exit {code}] {Truncate(error, 400)}";
                return output;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                return FormatCliError(code, error);
            }

            return $"[CLI ERROR: {Name}] Exit {code} with empty stdout/stderr.";
        }
        catch (Exception ex)
        {
            return $"[CLI ERROR: {Name}] Failed to start '{ExecutablePath}': {ex.Message}";
        }
        finally
        {
            // Ephemeral clean-up: delete the temporary prompt file immediately after C# receives response
            if (File.Exists(tempPromptFile))
            {
                try { File.Delete(tempPromptFile); } catch { }
            }
        }
    }

    private string FormatCliError(int code, string error)
    {
        string msg = Truncate(error, 800);
        // Surface quota / auth clearly — never dress these up as in-character speech.
        if (msg.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("subscription", StringComparison.OrdinalIgnoreCase))
        {
            return $"[CLI ERROR: {Name}] Quota/limit: {msg}";
        }

        if (msg.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("login", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("api key", StringComparison.OrdinalIgnoreCase))
        {
            return $"[CLI ERROR: {Name}] Auth: {msg}";
        }

        return $"[CLI ERROR: {Name}] Exit {code}: {msg}";
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "…";

    /// <summary>
    /// Ensure child processes can find tools in ~/.local/bin even when the GUI was
    /// launched with a stripped PATH.
    /// </summary>
    private static void EnsureExtendedPath(ProcessStartInfo psi)
    {
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extras = new List<string>();

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
            extras.Add(Path.Combine(home, ".local", "bin"));

        extras.Add("/usr/local/bin");
        extras.Add("/usr/bin");

        var parts = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var extra in extras)
        {
            if (!parts.Any(p => string.Equals(p, extra, StringComparison.Ordinal)))
                parts.Insert(0, extra);
        }

        psi.Environment["PATH"] = string.Join(Path.PathSeparator, parts);
        psi.Environment["PYTHONUNBUFFERED"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["TERM"] = "dumb";
    }

    /// <summary>
    /// Build argv from a template where <c>{0}</c> is replaced by the full prompt as one argument.
    /// </summary>
    internal static void ApplyArguments(ProcessStartInfo psi, string template, string prompt)
    {
        template = string.IsNullOrWhiteSpace(template) ? "-p {0}" : template.Trim();

        // Normalize legacy quoted form: -p "{0}"  →  -p {0}
        template = template.Replace("\"{0}\"", "{0}");

        int idx = template.IndexOf("{0}", StringComparison.Ordinal);
        if (idx < 0)
        {
            foreach (var token in SplitTokens(template))
                psi.ArgumentList.Add(token);
            psi.ArgumentList.Add(prompt);
            return;
        }

        string before = template.Substring(0, idx);
        string after = template.Substring(idx + 3);

        foreach (var token in SplitTokens(before))
            psi.ArgumentList.Add(token);

        psi.ArgumentList.Add(prompt);

        foreach (var token in SplitTokens(after))
            psi.ArgumentList.Add(token);
    }

    private static IEnumerable<string> SplitTokens(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            yield break;

        foreach (var token in segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string t = token.Trim().Trim('"');
            if (t.Length > 0)
                yield return t;
        }
    }
}
