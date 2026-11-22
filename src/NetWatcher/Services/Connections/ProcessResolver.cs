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
    private const long PidCacheTtlMs = 2500;

    private readonly ConcurrentDictionary<ProcessKey, ProcessInfo> _cache = new();
    private readonly ConcurrentDictionary<int, ProcessKey> _lastKeys = new();
    private readonly ConcurrentDictionary<int, long> _checkedAt = new();

    /// <summary>
    /// Returns metadata for the live process with <paramref name="pid"/>, or the
    /// cached entry for its last known instance once it has exited. A successful
    /// resolution is reused for <see cref="PidCacheTtlMs"/>, so a busy PID costs
    /// at most one process query per interval instead of one per event.
    /// </summary>
    public ProcessInfo GetOrAdd(int pid)
    {
        if (_cache.Count > 65_536)
        {
            // Session-long caches must not grow without bound on a host that
            // starts and stops processes for days; a miss simply re-resolves.
            _cache.Clear();
            _lastKeys.Clear();
            _checkedAt.Clear();
        }

        var now = Environment.TickCount64;

        if (_checkedAt.TryGetValue(pid, out var checkedAt) &&
            now - checkedAt < PidCacheTtlMs &&
            _lastKeys.TryGetValue(pid, out var knownKey) &&
            _cache.TryGetValue(knownKey, out var known))
        {
            return known;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            var key = new ProcessKey(pid, process.StartTime.ToUniversalTime());
            _lastKeys[pid] = key;
            _checkedAt[pid] = now;

            return _cache.GetOrAdd(key, _ => CreateInfo(process, key));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception
                                       or NotSupportedException or UnauthorizedAccessException)
        {
            _checkedAt[pid] = now;

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
