using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using NetWatcher.Models;

namespace NetWatcher.Services.Connections;

/// <summary>
/// Resolves a PID to process metadata and caches it keyed by
/// <see cref="ProcessKey"/>, so a recycled PID never inherits the old name.
/// </summary>
public sealed class ProcessResolver
{
    private readonly ConcurrentDictionary<ProcessKey, ProcessInfo> _cache = new();
    private readonly ConcurrentDictionary<int, ProcessKey> _lastKeys = new();

    /// <summary>
    /// Returns metadata for the live process with <paramref name="pid"/>, or the
    /// cached entry for its last known instance once it has exited.
    /// </summary>
    public ProcessInfo GetOrAdd(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var key = new ProcessKey(pid, process.StartTime.ToUniversalTime());
            _lastKeys[pid] = key;

            return _cache.GetOrAdd(key, _ => CreateInfo(process, key));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            if (_lastKeys.TryGetValue(pid, out var lastKey) &&
                _cache.TryGetValue(lastKey, out var cached))
            {
                return cached;
            }

            return new ProcessInfo(new ProcessKey(pid, DateTime.MinValue), $"pid:{pid}", null);
        }
    }

    /// <summary>Returns the cached entry for exactly <paramref name="key"/>, or null.</summary>
    public ProcessInfo? GetCached(ProcessKey key) =>
        _cache.TryGetValue(key, out var info) ? info : null;

    private static ProcessInfo CreateInfo(Process process, ProcessKey key)
    {
        string name;
        try
        {
            name = process.ProcessName;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            name = $"pid:{key.Pid}";
        }

        string? path = null;
        try
        {
            path = process.MainModule?.FileName;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // Protected processes deny module access; the name still identifies them.
        }

        return new ProcessInfo(key, name, path);
    }
}
