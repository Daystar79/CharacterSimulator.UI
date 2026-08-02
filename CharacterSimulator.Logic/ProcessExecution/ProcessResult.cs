using System;

namespace CharacterSimulator.Logic.ProcessExecution;

/// <summary>
/// Classification of process exit reasons for better error handling
/// </summary>
public enum ProcessExitReason
{
    /// <summary>Process completed successfully</summary>
    Success,
    /// <summary>Process was terminated due to timeout</summary>
    Timeout,
    /// <summary>Process was cancelled by user request</summary>
    Cancelled,
    /// <summary>Executable file was not found</summary>
    FileNotFound,
    /// <summary>Permission was denied to execute</summary>
    PermissionDenied,
    /// <summary>API quota was exceeded</summary>
    QuotaExceeded,
    /// <summary>Rate limiting was applied</summary>
    RateLimited,
    /// <summary>Network error occurred</summary>
    NetworkError,
    /// <summary>Process crashed or had internal error</summary>
    ProcessError,
    /// <summary>Unknown error occurred</summary>
    Unknown
}

/// <summary>
/// Result of a process execution with comprehensive error information
/// </summary>
public class ProcessResult
{
    /// <summary>Path to the executable that was run</summary>
    public string ExecutablePath { get; set; } = string.Empty;
    
    /// <summary>Exit code returned by the process</summary>
    public int ExitCode { get; set; }
    
    /// <summary>Standard output from the process</summary>
    public string StandardOutput { get; set; } = string.Empty;
    
    /// <summary>Standard error from the process</summary>
    public string StandardError { get; set; } = string.Empty;
    
    /// <summary>Whether the process exited successfully (exit code 0)</summary>
    public bool Success => ExitCode == 0 && ExitReason == ProcessExitReason.Success;
    
    /// <summary>Whether the process timed out</summary>
    public bool TimedOut { get; set; }
    
    /// <summary>Classification of why the process exited</summary>
    public ProcessExitReason ExitReason { get; set; } = ProcessExitReason.Unknown;
    
    /// <summary>How long the process took to execute</summary>
    public TimeSpan ExecutionTime { get; set; }
    
    /// <summary>Unique identifier for this execution</summary>
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");
    
    /// <summary>
    /// Categorizes the error based on stderr content and exit code
    /// </summary>
    public string ErrorCategory => ClassifyError(StandardError, ExitCode);
    
    /// <summary>
    /// User-provided error message (when not computed from exit code)
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Returns a computed error message based on exit code and reason
    /// </summary>
    public string ComputedErrorMessage => GetErrorMessage();
    
    private static string ClassifyError(string error, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(error) && exitCode == 0)
            return "Success";
            
        error = error.ToLowerInvariant();
        
        if (error.Contains("quota") || error.Contains("rate limit") || error.Contains("subscription"))
            return "RateLimit";
        if (error.Contains("permission") || error.Contains("access denied") || exitCode == 5)
            return "PermissionDenied";
        if (error.Contains("network") || error.Contains("connection") || error.Contains("dns"))
            return "NetworkError";
        if (error.Contains("file not found") || error.Contains("no such file") || exitCode == 2)
            return "FileNotFound";
        if (error.Contains("timeout") || error.Contains("timed out"))
            return "Timeout";
        if (exitCode < 0)
            return "ProcessError";
            
        return "Unknown";
    }
    
    private string GetErrorMessage()
    {
        if (Success) return "Process completed successfully";
        if (TimedOut) return "Process timed out";
        
        return ExitReason switch
        {
            ProcessExitReason.FileNotFound => $"Executable not found: {ExecutablePath}",
            ProcessExitReason.PermissionDenied => "Permission denied - check executable permissions",
            ProcessExitReason.QuotaExceeded => "API quota exceeded",
            ProcessExitReason.RateLimited => "Rate limit exceeded",
            ProcessExitReason.NetworkError => "Network error occurred",
            ProcessExitReason.ProcessError => $"Process crashed with exit code {ExitCode}",
            ProcessExitReason.Timeout => "Execution timed out",
            ProcessExitReason.Cancelled => "Execution was cancelled",
            _ => $"Process failed with exit code {ExitCode}"
        };
    }
    
    /// <summary>
    /// Returns the combined output (stdout + stderr) for debugging
    /// </summary>
    public string CombinedOutput => string.IsNullOrEmpty(StandardOutput) 
        ? StandardError 
        : string.IsNullOrEmpty(StandardError) 
            ? StandardOutput 
            : $"{StandardOutput}\n[STDERR]: {StandardError}";
}