using System.Net;
using NetWatcher.Utils;

namespace NetWatcher.Services.ReverseDns;

/// <summary>
/// Asynchronous PTR lookups for public addresses only, with a 24-hour cache,
/// a 20,000-entry cap, at most 4 concurrent queries, and a per-query timeout.
/// </summary>
public sealed class ReverseDnsResolver
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private const int MaxCacheEntries = 20_000;

    private readonly Func<IPAddress, CancellationToken, Task<string?>> _ptrQuery;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _gate = new(4, 4);
    private readonly object _sync = new();
    private readonly Dictionary<IPAddress, CacheEntry> _cache = new();

    public ReverseDnsResolver(
        Func<IPAddress, CancellationToken, Task<string?>>? ptrQuery = null,
        TimeProvider? timeProvider = null,
        TimeSpan timeout = default)
    {
        _ptrQuery = ptrQuery ?? DefaultQueryAsync;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timeout = timeout == default ? DefaultTimeout : timeout;
    }

    /// <summary>Returns the PTR host name, or null for private addresses, failures, and timeouts.</summary>
    public async Task<string?> ResolveAsync(IPAddress address, CancellationToken ct = default)
    {
        if (!IpHelpers.IsPublic(address))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        lock (_sync)
        {
            if (_cache.TryGetValue(address, out var cached) && cached.ExpiresUtc > now)
            {
                return cached.Name;
            }
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        string? name;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var query = _ptrQuery(address, cts.Token);
            var completed = await Task.WhenAny(query, Task.Delay(_timeout, ct)).ConfigureAwait(false);

            if (completed == query)
            {
                try
                {
                    name = await query.ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    name = null;
                }
            }
            else
            {
                cts.Cancel();
                name = null;
            }
        }
        finally
        {
            _gate.Release();
        }

        lock (_sync)
        {
            if (_cache.Count >= MaxCacheEntries)
            {
                Evict(now);
            }

            _cache[address] = new CacheEntry(name, now + CacheTtl);
        }

        return name;
    }

    private static async Task<string?> DefaultQueryAsync(IPAddress address, CancellationToken ct)
    {
        var entry = await Dns.GetHostEntryAsync(address).WaitAsync(ct).ConfigureAwait(false);
        return entry.HostName;
    }

    private void Evict(DateTime nowUtc)
    {
        foreach (var key in _cache
                     .Where(pair => pair.Value.ExpiresUtc <= nowUtc)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _cache.Remove(key);
        }

        if (_cache.Count < MaxCacheEntries)
        {
            return;
        }

        var oldest = _cache.MinBy(pair => pair.Value.ExpiresUtc).Key;
        _cache.Remove(oldest);
    }

    private readonly record struct CacheEntry(string? Name, DateTime ExpiresUtc);
}
