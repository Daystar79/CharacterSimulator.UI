using System;
using System.Collections.Generic;
using System.Linq;

namespace CharacterSimulator.Logic.Services;

public class BusyTaskToken : IDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    public BusyTaskToken(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _onDispose();
    }
}

/// <summary>
/// Global application task tracking service.
/// Displays visual thinking popups / indicators when async operations run.
/// </summary>
public static class BusyTaskService
{
    private static readonly object SyncLock = new();
    private static readonly Dictionary<string, string> ActiveTasks = new(StringComparer.OrdinalIgnoreCase);

    public static event Action? OnTaskStateChanged;

    public static bool IsBusy
    {
        get
        {
            lock (SyncLock)
                return ActiveTasks.Count > 0;
        }
    }

    public static string ActiveTaskText
    {
        get
        {
            lock (SyncLock)
            {
                if (ActiveTasks.Count == 0) return "";
                return ActiveTasks.Values.Last();
            }
        }
    }

    public static IDisposable BeginTask(string taskId, string description)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            taskId = Guid.NewGuid().ToString("N");

        lock (SyncLock)
        {
            ActiveTasks[taskId] = description;
        }
        OnTaskStateChanged?.Invoke();
        return new BusyTaskToken(() => EndTask(taskId));
    }

    public static void EndTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return;

        lock (SyncLock)
        {
            ActiveTasks.Remove(taskId);
        }
        OnTaskStateChanged?.Invoke();
    }

    public static void ClearAll()
    {
        lock (SyncLock)
        {
            ActiveTasks.Clear();
        }
        OnTaskStateChanged?.Invoke();
    }
}
