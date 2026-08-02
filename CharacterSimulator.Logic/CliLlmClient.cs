using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CharacterSimulator.Logic.ProcessExecution;

namespace CharacterSimulator.Logic;

/// <summary>
/// CLI-based LLM client that executes external LLM providers
/// </summary>
public class CliLlmClient : ILLMClient, IDisposable
{
    public string Name { get; }
    public string ExecutablePath { get; }
    /// <summary>
    /// Argument template. Use <c>{0}</c> where the prompt should be inserted as a single argv entry.
    /// Example: <c>-p {0} --auto-approve --output text</c>
    /// </summary>
    public string ArgumentsTemplate { get; }
    public int TimeoutMs { get; set; } = 180_000;
    public int MaxRetries { get; set; } = 2;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    
    private readonly ProcessExecutor _executor;
    private readonly object _pathLock = new object();
    private string _extendedPath;
    private bool _disposed = false;
    
    /// <summary>
    /// Initializes a new CliLlmClient
    /// </summary>
    /// <param name="name">Client name for identification</param>
    /// <param name="executablePath">Path to the CLI executable</param>
    /// <param name="argumentsTemplate">Argument template with {0} for prompt</param>
    public CliLlmClient(string name, string executablePath, string argumentsTemplate = "-p {0}")
    {
        Name = name ?? "CLI_LLM";
        ExecutablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        ArgumentsTemplate = argumentsTemplate ?? "-p {0}";
        
        try
        {
            _executor = new ProcessExecutor(
                executablePath, 
                argumentsTemplate, 
                TimeSpan.FromMilliseconds(TimeoutMs));
        }
        catch (FileNotFoundException)
        {
            // Allow creation of client even if executable doesn't exist
            // This allows for deferred error handling during actual execution
            _executor = null;
        }
    }
    
    /// <summary>
    /// Synchronous prompt execution (backward compatibility)
    /// </summary>
    public string SendPrompt(Character character, string input, string sceneContext, string goalContext = "")
    {
        // Handle case where executor wasn't created (executable not found)
        if (_executor == null)
        {
            return $"[CLI ERROR: {Name}] Executable not found: '{ExecutablePath}'. " +
                   "Check that it is installed and visible on PATH (GUI apps may miss ~/.local/bin).";
        }
        
        return SendPromptAsync(character, input, sceneContext, goalContext, CancellationToken.None)
            .GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Asynchronous prompt execution with cancellation support
    /// </summary>
    public async Task<string> SendPromptAsync(Character character, string input, string sceneContext, 
        string goalContext = "", CancellationToken ct = default)
    {
        // Handle case where executor wasn't created (executable not found)
        if (_executor == null)
        {
            return $"[CLI ERROR: {Name}] Executable not found: '{ExecutablePath}'. " +
                   "Check that it is installed and visible on PATH (GUI apps may miss ~/.local/bin).";
        }
        
        string prompt = PromptBuilder.BuildFullPrompt(character, input, sceneContext, goalContext);
        return await ExecuteWithRetryAsync(prompt, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Free-form completion without RP prompt assembly (card builders, tools).
    /// </summary>
    public Task<string> CompleteRawAsync(string prompt, CancellationToken ct = default)
    {
        if (_executor == null)
        {
            return Task.FromResult(
                $"[CLI ERROR: {Name}] Executable not found: '{ExecutablePath}'. " +
                "Check that it is installed and visible on PATH (GUI apps may miss ~/.local/bin).");
        }

        if (string.IsNullOrWhiteSpace(prompt))
            return Task.FromResult("[CLI ERROR] Empty prompt.");

        return ExecuteWithRetryAsync(prompt, ct);
    }
    
    /// <summary>
    /// Executes the CLI with retry logic
    /// </summary>
    private async Task<string> ExecuteWithRetryAsync(string prompt, CancellationToken ct)
    {
        Exception lastException = null;
        
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await _executor.ExecuteAsync(prompt, ct).ConfigureAwait(false);
                
                if (result.TimedOut && attempt < MaxRetries)
                {
                    lastException = new TimeoutException($"Attempt {attempt + 1} timed out");
                    await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
                    continue;
                }
                
                if (result.Success)
                {
                    return CleanResponse(result.StandardOutput);
                }
                else
                {
                    return FormatProcessResult(result);
                }
            }
            catch (OperationCanceledException)
            {
                // Re-throw cancellation
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
                    continue;
                }
                return FormatExceptionError(ex);
            }
        }
        
        return FormatMaxRetriesError(lastException);
    }
    
    /// <summary>
    /// Formats the process result into a response string
    /// </summary>
    private string FormatProcessResult(ProcessResult result)
    {
        if (string.IsNullOrWhiteSpace(result.StandardOutput) && 
            string.IsNullOrWhiteSpace(result.StandardError))
        {
            return result.ExitReason switch
            {
                ProcessExitReason.FileNotFound => 
                    $"[CLI ERROR: {Name}] Executable not found: '{ExecutablePath}'. " +
                    "Check that it is installed and visible on PATH (GUI apps may miss ~/.local/bin).",
                ProcessExitReason.PermissionDenied => 
                    $"[CLI ERROR: {Name}] Permission denied executing '{Path.GetFileName(ExecutablePath)}'.",
                ProcessExitReason.Timeout => 
                    $"[CLI ERROR: {Name}] Timed out after {TimeoutMs / 1000}s waiting for '{Path.GetFileName(ExecutablePath)}'. " +
                    "The provider may be waiting for tool approval or network. Try again or use Mock.",
                ProcessExitReason.Cancelled => 
                    $"[CLI ERROR: {Name}] Operation cancelled by user request.",
                _ => 
                    $"[CLI ERROR: {Name}] Exit {result.ExitCode} with empty stdout/stderr."
            };
        }
        
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            // Some CLIs write warnings to stderr but still produce a good answer
            if (result.ExitCode != 0 && !string.IsNullOrWhiteSpace(result.StandardError))
                return result.StandardOutput + $"\n[CLI note: exit {result.ExitCode}] {Truncate(result.StandardError, 400)}";
            return result.StandardOutput;
        }
        
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            return FormatCliError(result.ExitCode, result.StandardError);
        }
        
        return result.ErrorMessage ?? $"[CLI ERROR: {Name}] Unknown error with exit code {result.ExitCode}";
    }
    
    /// <summary>
    /// Formats an exception into an error string
    /// </summary>
    private string FormatExceptionError(Exception ex)
    {
        return ex switch
        {
            TimeoutException => $"[CLI ERROR: {Name}] Request timed out after multiple attempts",
            FileNotFoundException fnf => $"[CLI ERROR: {Name}] {fnf.Message}",
            OperationCanceledException => $"[CLI ERROR: {Name}] Operation was cancelled",
            _ => $"[CLI ERROR: {Name}] Unexpected error: {ex.Message}"
        };
    }
    
    /// <summary>
    /// Formats max retries exceeded error
    /// </summary>
    private string FormatMaxRetriesError(Exception lastException)
    {
        return $"[CLI ERROR: {Name}] Failed after {MaxRetries + 1} attempts. " +
               (lastException != null ? lastException.Message : "Unknown error");
    }
    
    /// <summary>
    /// Cleans the response by removing sensitive information
    /// </summary>
    private string CleanResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;
            
        // Remove potential system leaks from the response
        var leakAudit = Hygiene.SystemLeakLinter.Audit(response);
        return leakAudit.SanitizedDialogue;
    }
    
    /// <summary>
    /// Ensures extended PATH is available (cached)
    /// </summary>
    private string GetExtendedPath()
    {
        if (_extendedPath != null) return _extendedPath;
        
        lock (_pathLock)
        {
            if (_extendedPath != null) return _extendedPath;
            
            _extendedPath = BuildExtendedPath();
            return _extendedPath;
        }
    }
    
    /// <summary>
    /// Builds the extended PATH with common CLI tool locations
    /// </summary>
    private static string BuildExtendedPath()
    {
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var parts = new List<string>(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
            parts.Insert(0, Path.Combine(home, ".local", "bin"));
        
        parts.Insert(0, "/usr/local/bin");
        parts.Insert(0, "/usr/bin");
        
        // Remove duplicates while preserving order
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueParts = new List<string>();
        foreach (var part in parts)
        {
            if (seen.Add(part))
                uniqueParts.Add(part);
        }
        
        return string.Join(Path.PathSeparator, uniqueParts);
    }
    
    /// <summary>
    /// Formats CLI error message based on exit code and error output
    /// </summary>
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
    
    /// <summary>
    /// Truncates a string to maximum length
    /// </summary>
    private static string Truncate(string s, int max)
    {
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }
    
    /// <summary>
    /// Tests if this client is working properly
    /// </summary>
    public async Task<bool> TestAsync(TimeSpan? timeout = null)
    {
        return _executor != null && await LlmClientHealthCheck.TestClientAsync(_executor, timeout).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Gets the status of this client
    /// </summary>
    public string GetStatus()
    {
        return _executor != null ? LlmClientHealthCheck.GetClientStatus(_executor) 
            : $"Executable not found: {ExecutablePath}";
    }
    
    /// <summary>
    /// Gets detailed status information
    /// </summary>
    public LlmClientStatus GetDetailedStatus()
    {
        return _executor != null ? LlmClientHealthCheck.GetDetailedStatus(_executor) 
            : new LlmClientStatus { Status = ClientStatus.ExecutableNotFound, Message = $"Executable not found: {ExecutablePath}" };
    }
    
    /// <summary>
    /// Gets version information for the CLI executable
    /// </summary>
    public FileVersionInfo GetVersionInfo()
    {
        return _executor?.GetVersionInfo();
    }
    
    /// <summary>
    /// Validates that the executable exists and is accessible
    /// </summary>
    public bool ValidateExecutable()
    {
        return _executor?.ValidateExecutable() ?? false;
    }
    
    /// <summary>
    /// Applies arguments template to the prompt (for external use)
    /// </summary>
    internal static void ApplyArguments(ProcessStartInfo psi, string template, string prompt)
    {
        template = string.IsNullOrWhiteSpace(template) ? "-p {0}" : template.Trim();
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
    
    /// <summary>
    /// Disposes the client and releases resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _executor?.Dispose();
            _disposed = true;
        }
    }
    
    /// <summary>
    /// Finalizer for safety
    /// </summary>
    ~CliLlmClient()
    {
        Dispose();
    }
}

/// <summary>
/// Extension methods for ILLMClient
/// </summary>
public static class LlmClientExtensions
{
    /// <summary>
    /// Tests if a client is working
    /// </summary>
    public static async Task<bool> TestAsync(this ILLMClient client, TimeSpan? timeout = null)
    {
        if (client is CliLlmClient cliClient)
        {
            return await cliClient.TestAsync(timeout).ConfigureAwait(false);
        }
        
        // For MockLLMClient, just return true
        return client is MockLLMClient;
    }
    
    /// <summary>
    /// Gets status of a client
    /// </summary>
    public static string GetStatus(this ILLMClient client)
    {
        if (client is CliLlmClient cliClient)
        {
            return cliClient.GetStatus();
        }
        
        return client is MockLLMClient ? "Mock/Ready" : "Unknown";
    }
}