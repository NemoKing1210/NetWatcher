using System.Net;
using NetWatcher.Models;

namespace NetWatcher.Storage;

/// <summary>
/// Thread-safe in-memory store for session connections and DNS records with
/// deduplication, bounded size, and time-window lookups used by the correlator.
/// </summary>
public sealed class NetworkStore
{
    private static readonly TimeSpan DnsDedupWindow = TimeSpan.FromSeconds(30);

    private readonly object _sync = new();
    private readonly Dictionary<ConnectionKey, ConnectionRecord> _connections = new();
    private readonly List<DnsRecord> _dns = new();
    private readonly Dictionary<IPAddress, List<DnsRecord>> _dnsIndex = new();

    /// <summary>Adds or merges a connection, keeping the first-seen time.</summary>
    public void UpsertConnection(ConnectionRecord record)
    {
        var key = ConnectionKey.From(record);

        lock (_sync)
        {
            if (_connections.TryGetValue(key, out var existing))
            {
                _connections[key] = existing with
                {
                    LastSeenUtc = record.LastSeenUtc > existing.LastSeenUtc
                        ? record.LastSeenUtc
                        : existing.LastSeenUtc,
                    State = record.State ?? existing.State,
                };
            }
            else
            {
                _connections[key] = record;
            }
        }
    }

    /// <summary>Adds a DNS answer, merging repeats of the same (PID, name, IP) within 30 s.</summary>
    public void AddDns(DnsRecord record)
    {
        lock (_sync)
        {
            var existing = _dns.FirstOrDefault(r =>
                r.Process == record.Process &&
                r.QueryName == record.QueryName &&
                (record.TimestampUtc - r.TimestampUtc).Duration() <= DnsDedupWindow &&
                r.Addresses.Intersect(record.Addresses).Any());

            if (existing is null)
            {
                _dns.Add(record);
                AddToIndex(record);
                return;
            }

            var merged = existing with
            {
                Addresses = existing.Addresses.Union(record.Addresses).ToArray(),
                TimestampUtc = record.TimestampUtc > existing.TimestampUtc
                    ? record.TimestampUtc
                    : existing.TimestampUtc,
                Status = record.Status,
            };

            _dns[_dns.IndexOf(existing)] = merged;
            RemoveFromIndex(existing);
            AddToIndex(merged);
        }
    }

    /// <summary>Returns an immutable copy of the current connections.</summary>
    public IReadOnlyList<ConnectionRecord> GetConnectionSnapshot()
    {
        lock (_sync)
        {
            return _connections.Values.ToArray();
        }
    }

    /// <summary>Returns an immutable copy of the current DNS records.</summary>
    public IReadOnlyList<DnsRecord> GetDnsSnapshot()
    {
        lock (_sync)
        {
            return _dns.ToArray();
        }
    }

    /// <summary>
    /// The most recent DNS answer for this process and address within the window,
    /// or null when there is none.
    /// </summary>
    public DnsRecord? FindBestDns(
        ProcessKey process,
        IPAddress address,
        DateTime minUtc,
        DateTime maxUtc)
    {
        lock (_sync)
        {
            return FindBestDnsCore(process, address, minUtc, maxUtc);
        }
    }

    /// <summary>
    /// The most recent DNS answer for this address within the window regardless
    /// of process, or null when there is none (the shared/CDN case).
    /// </summary>
    public DnsRecord? FindBestDnsGlobal(IPAddress address, DateTime minUtc, DateTime maxUtc)
    {
        lock (_sync)
        {
            return FindBestDnsCore(process: null, address, minUtc, maxUtc);
        }
    }

    /// <summary>
    /// Evicts the oldest connections and DNS records beyond the caps.
    /// Returns the number of records removed.
    /// </summary>
    public int EvictByLastSeen(int maxConnections, int maxDns)
    {
        lock (_sync)
        {
            var evicted = 0;

            if (_connections.Count > maxConnections)
            {
                var keys = _connections
                    .OrderBy(pair => pair.Value.LastSeenUtc)
                    .Take(_connections.Count - maxConnections)
                    .Select(pair => pair.Key)
                    .ToArray();

                foreach (var key in keys)
                {
                    _connections.Remove(key);
                    evicted++;
                }
            }

            if (_dns.Count > maxDns)
            {
                var oldest = _dns
                    .OrderBy(record => record.TimestampUtc)
                    .Take(_dns.Count - maxDns)
                    .ToArray();

                foreach (var record in oldest)
                {
                    _dns.Remove(record);
                    RemoveFromIndex(record);
                    evicted++;
                }
            }

            return evicted;
        }
    }

    /// <summary>Drops all session data.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _connections.Clear();
            _dns.Clear();
            _dnsIndex.Clear();
        }
    }

    private DnsRecord? FindBestDnsCore(
        ProcessKey? process,
        IPAddress address,
        DateTime minUtc,
        DateTime maxUtc)
    {
        if (!_dnsIndex.TryGetValue(address, out var candidates))
        {
            return null;
        }

        DnsRecord? best = null;
        foreach (var record in candidates)
        {
            if (record.TimestampUtc < minUtc || record.TimestampUtc > maxUtc)
            {
                continue;
            }

            if (process is not null && record.Process != process)
            {
                continue;
            }

            if (best is null || record.TimestampUtc > best.TimestampUtc)
            {
                best = record;
            }
        }

        return best;
    }

    private void AddToIndex(DnsRecord record)
    {
        foreach (var address in record.Addresses)
        {
            if (!_dnsIndex.TryGetValue(address, out var list))
            {
                list = new List<DnsRecord>();
                _dnsIndex[address] = list;
            }

            list.Add(record);
        }
    }

    private void RemoveFromIndex(DnsRecord record)
    {
        foreach (var address in record.Addresses)
        {
            if (_dnsIndex.TryGetValue(address, out var list))
            {
                list.Remove(record);
                if (list.Count == 0)
                {
                    _dnsIndex.Remove(address);
                }
            }
        }
    }

    private readonly record struct ConnectionKey(
        ProcessKey Process,
        Protocol Protocol,
        IPAddress Remote,
        int RemotePort,
        int LocalPort,
        bool Inbound)
    {
        public static ConnectionKey From(ConnectionRecord record) => new(
            record.Process,
            record.Protocol,
            record.RemoteAddress,
            record.RemotePort,
            record.LocalPort,
            record.Inbound);
    }
}
