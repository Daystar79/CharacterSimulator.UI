using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.ProcessExecution;

/// <summary>
/// Pool of process executors for efficient reuse
/// </summary>
public class ProcessPool : IDisposable
{
    private readonly ConcurrentDictionary<string, Queue<ProcessExecutor>> _pool = new();
    private readonly ConcurrentDictionary<string, int> _activeCount = new();
    private readonly SemaphoreSlim _creationLock = new(1, 1);
    private readonly TimeSpan _executorTimeout;
    private readonly int _maxPoolSize;
    
    private bool _disposed = false;
    private readonly object _disposeLock = new object();
    
    /// <summary>
    /// Initializes a new ProcessPool
    /// </summary>
    /// <param name="executorTimeout">Default timeout for process executors</param>
    /// <param name="maxPoolSize">Maximum number of executors per key (default: 5)</param>
    public ProcessPool(TimeSpan executorTimeout, int maxPoolSize = 5)
    {
        _executorTimeout = executorTimeout;
        _maxPoolSize = Math.Max(1, maxPoolSize);
    }
    
    /// <summary>
    /// Gets or creates a ProcessExecutor for the given key
    /// </summary>
    /// <param name="key">Unique key for this executor type</param>
    /// <param name="executablePath">Path to the executable</param>
    /// <param name="argumentsTemplate">Arguments template</param>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>ProcessExecutor instance</returns>
    public async ValueTask<ProcessExecutor> GetExecutorAsync(
        string key, 
        string executablePath,
        string argumentsTemplate = null,
        string workingDirectory = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ProcessPool));
            
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException("Key and executablePath cannot be null or empty");
            
        // Try to get from pool first
        if (_pool.TryGetValue(key, out var queue) && queue.TryDequeue(out var executor))
        {
            IncrementActiveCount(key);
            return executor;
        }
        
        // Create new executor
        await _creationLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_pool.TryGetValue(key, out queue) && queue.TryDequeue(out executor))
            {
                IncrementActiveCount(key);
                return executor;
            }
            
            // Create new executor
            executor = new ProcessExecutor(
                executablePath, 
                argumentsTemplate ?? "-p {0}", 
                _executorTimeout,
                workingDirectory);
                
            IncrementActiveCount(key);
            return executor;
        }
        finally
        {
            _creationLock.Release();
        }
    }
    
    /// <summary>
    /// Returns a ProcessExecutor to the pool
    /// </summary>
    /// <param name="key">The key this executor was obtained with</param>
    /// <param name="executor">The executor to return</param>
    public void ReturnExecutor(string key, ProcessExecutor executor)
    {
        if (_disposed || string.IsNullOrEmpty(key) || executor == null)
            return;
            
        // Decrement active count
        DecrementActiveCount(key);
        
        // Don't pool if we have too many
        if (!_pool.TryGetValue(key, out var queue) || queue.Count >= _maxPoolSize)
        {
            executor.Dispose();
            return;
        }
        
        // Add to pool
        _pool.AddOrUpdate(key, 
            new Queue<ProcessExecutor>(new[] { executor }),
            (k, existingQueue) => 
            {
                existingQueue.Enqueue(executor);
                return existingQueue;
            });
    }
    
    /// <summary>
    /// Gets the number of active executors for a given key
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <returns>Number of active executors</returns>
    public int GetActiveCount(string key)
    {
        if (_activeCount.TryGetValue(key, out var count))
            return count;
        return 0;
    }
    
    /// <summary>
    /// Gets the total number of pooled executors
    /// </summary>
    public int TotalPooledExecutors
    {
        get
        {
            int total = 0;
            foreach (var queue in _pool.Values)
            {
                lock (queue)
                {
                    total += queue.Count;
                }
            }
            return total;
        }
    }
    
    /// <summary>
    /// Gets all active keys
    /// </summary>
    public IEnumerable<string> Keys => _pool.Keys;
    
    private void IncrementActiveCount(string key)
    {
        _activeCount.AddOrUpdate(key, 1, (k, current) => current + 1);
    }
    
    private void DecrementActiveCount(string key)
    {
        _activeCount.AddOrUpdate(key, 0, (k, current) => Math.Max(0, current - 1));
    }
    
    /// <summary>
    /// Clears the pool, disposing all executors
    /// </summary>
    public void Clear()
    {
        foreach (var queue in _pool.Values)
        {
            foreach (var executor in queue)
            {
                try { executor.Dispose(); } catch { }
            }
        }
        _pool.Clear();
        _activeCount.Clear();
    }
    
    /// <summary>
    /// Removes all executors for a specific key
    /// </summary>
    /// <param name="key">The key to remove</param>
    public void Clear(string key)
    {
        if (_pool.TryRemove(key, out var queue))
        {
            foreach (var executor in queue)
            {
                try { executor.Dispose(); } catch { }
            }
        }
        _activeCount.TryRemove(key, out _);
    }
    
    /// <summary>
    /// Disposes the pool and all contained executors
    /// </summary>
    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (!_disposed)
            {
                // Wait for active processes to complete
                foreach (var key in _activeCount.Keys)
                {
                    while (_activeCount.TryGetValue(key, out var count) && count > 0)
                    {
                        Thread.Sleep(100);
                    }
                }
                
                Clear();
                _creationLock.Dispose();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Gets statistics about the pool
    /// </summary>
    /// <returns>Pool statistics</returns>
    public ProcessPoolStatistics GetStatistics()
    {
        return new ProcessPoolStatistics
        {
            TotalPooled = TotalPooledExecutors,
            ActiveExecutors = GetTotalActiveCount(),
            TotalKeys = _pool.Count
        };
    }
    
    private int GetTotalActiveCount()
    {
        int total = 0;
        foreach (var count in _activeCount.Values)
        {
            total += count;
        }
        return total;
    }
}

/// <summary>
/// Statistics for a ProcessPool
/// </summary>
public class ProcessPoolStatistics
{
    /// <summary>Total number of pooled executors</summary>
    public int TotalPooled { get; set; }
    
    /// <summary>Total number of active executors</summary>
    public int ActiveExecutors { get; set; }
    
    /// <summary>Total number of keys</summary>
    public int TotalKeys { get; set; }
}