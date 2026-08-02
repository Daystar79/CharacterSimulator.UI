using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.ProcessExecution;

/// <summary>
/// Provides memory-efficient streaming for process output
/// </summary>
public static class StreamingProcessReader
{
    private const int DefaultBufferSize = 4096;
    private const int MaxOutputSize = 10 * 1024 * 1024; // 10MB limit
    
    /// <summary>
    /// Reads all output from a TextReader asynchronously with streaming
    /// </summary>
    /// <param name="reader">The TextReader to read from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="bufferSize">Buffer size for reading (default: 4096)</param>
    /// <returns>The complete output as a string</returns>
    public static async Task<string> ReadToEndAsync(
        TextReader reader, 
        CancellationToken cancellationToken = default,
        int bufferSize = DefaultBufferSize)
    {
        if (reader == null) return string.Empty;
        
        var buffer = new char[bufferSize];
        var result = new StringBuilder();
        int bytesRead;
        
        while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Append(buffer, 0, bytesRead);
            
            // Prevent excessive memory usage
            if (result.Length > MaxOutputSize)
            {
                result.Append("\n[OUTPUT TRUNCATED - Maximum size exceeded]");
                break;
            }
        }
        
        return result.ToString();
    }
    
    /// <summary>
    /// Reads all output from a Stream asynchronously with streaming
    /// </summary>
    /// <param name="stream">The Stream to read from</param>
    /// <param name="encoding">The encoding to use (default: UTF-8)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="bufferSize">Buffer size for reading (default: 4096)</param>
    /// <returns>The complete output as a string</returns>
    public static async Task<string> ReadToEndAsync(
        Stream stream, 
        Encoding encoding = null,
        CancellationToken cancellationToken = default,
        int bufferSize = DefaultBufferSize)
    {
        if (stream == null) return string.Empty;
        
        encoding ??= Encoding.UTF8;
        var buffer = new byte[bufferSize];
        var result = new StringBuilder();
        int bytesRead;
        
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Append(encoding.GetString(buffer, 0, bytesRead));
            
            // Prevent excessive memory usage
            if (result.Length > MaxOutputSize)
            {
                result.Append("\n[OUTPUT TRUNCATED - Maximum size exceeded]");
                break;
            }
        }
        
        return result.ToString();
    }
    
    /// <summary>
    /// Reads output line by line with callback for real-time processing
    /// </summary>
    /// <param name="reader">The TextReader to read from</param>
    /// <param name="lineCallback">Callback for each line (line content, line number)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total number of lines read</returns>
    public static async Task<int> ReadLinesAsync(
        TextReader reader,
        Func<string, long, Task> lineCallback,
        CancellationToken cancellationToken = default)
    {
        if (reader == null) return 0;
        
        string line;
        long lineNumber = 0;
        
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await lineCallback(line, lineNumber++).ConfigureAwait(false);
            
            if (lineNumber > 100000) // Safety limit
            {
                await lineCallback("[OUTPUT TRUNCATED - Maximum lines exceeded]", lineNumber).ConfigureAwait(false);
                break;
            }
        }
        
        return (int)lineNumber;
    }
    
    /// <summary>
    /// Creates a buffered reader that can be used for streaming large outputs
    /// </summary>
    /// <param name="stream">The underlying stream</param>
    /// <param name="encoding">The encoding to use</param>
    /// <param name="bufferSize">Buffer size</param>
    /// <returns>A buffered TextReader</returns>
    public static TextReader CreateBufferedReader(
        Stream stream, 
        Encoding encoding = null,
        int bufferSize = DefaultBufferSize)
    {
        encoding ??= Encoding.UTF8;
        return new StreamReader(stream, encoding, true, bufferSize);
    }
    
    /// <summary>
    /// Writes input to a process stdin with chunking for large inputs
    /// </summary>
    /// <param name="writer">The StreamWriter to write to</param>
    /// <param name="input">The input to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="chunkSize">Chunk size for writing (default: 4096)</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task WriteToStdinAsync(
        StreamWriter writer, 
        string input,
        CancellationToken cancellationToken = default,
        int chunkSize = DefaultBufferSize)
    {
        if (writer == null || string.IsNullOrEmpty(input)) return;
        
        for (int i = 0; i < input.Length; i += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = Math.Min(chunkSize, input.Length - i);
            string chunk = input.Substring(i, length);
            await writer.WriteAsync(chunk).ConfigureAwait(false);
        }
        
        await writer.FlushAsync().ConfigureAwait(false);
    }
}