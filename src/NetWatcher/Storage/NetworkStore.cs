using System.Net;
using NetWatcher.Models;

namespace NetWatcher.Storage;

/// <summary>
/// Thread-safe in-memory store for session connections and DNS records with
/// deduplication, bounded size, and time-window lookups used by the correlator.
/// Deduplication and eviction use secondary indexes and lazy priority queues, so
/// neither needs a linear scan of the whole store.
/// </summary>
public sealed class NetworkStore
{
    private static readonly TimeSpan DnsDedupWindow = TimeSpan.FromSeconds(30);

    private readonly object _sync = new();

    private readonly Dictionary<ConnectionIdentity, ConnectionRecord> _connections = new();
    private readonly Dictionary<ConnectionIdentity, int> _connectionVersions = new();
    private readonly PriorityQueue<(ConnectionIdentity Key, int Version), DateTime> _connectionEviction = new();

    private readonly Dictionary<long, DnsEntry> _dns = new();
    private readonly Dictionary<(ProcessKey Process, string Name), List<long>> _dnsByName = new();
    private readonly Dictionary<IPAddress, HashSet<long>> _dnsIndex = new();
    private readonly PriorityQueue<(long Id, int Version), DateTime> _dnsEviction = new();
    private long _nextDnsId;

    /// <summary>Adds or merges a connection, keeping the earliest first-seen time.</summary>
    public void UpsertConnection(ConnectionRecord record)
    {
        var key = ConnectionIdentity.From(record);

        lock (_sync)
        {
            if (_connections.TryGetValue(key, out var existing))
            {
                _connections[key] = existing with
                {
                    FirstSeenUtc = record.FirstSeenUtc < existing.FirstSeenUtc
                        ? record.FirstSeenUtc
                        : existing.FirstSeenUtc,
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

            var version = NextConnectionVersion(key);
            _connectionEviction.Enqueue((key, version), _connections[key].LastSeenUtc);
            CompactConnectionEviction();
        }
    }

    /// <summary>
    /// Adds a DNS answer, merging repeats of the same (PID, name, address) within
    /// 30 s. The merged record keeps the earliest timestamp so a later repeat
    /// cannot push a still-open connection out of its correlation window.
    /// </summary>
    public void AddDns(DnsRecord record)
    {
        lock (_sync)
        {
            var nameKey = (record.Process, record.QueryName);

            if (_dnsByName.TryGetValue(nameKey, out var ids))
            {
                foreach (var id in ids)
                {
                    var entry = _dns[id];
                    var candidate = entry.Record;

                    if ((record.TimestampUtc - candidate.TimestampUtc).Duration() > DnsDedupWindow ||
                        !CanMerge(candidate.Addresses, record.Addresses))
                    {
                        continue;
                    }

                    var merged = candidate with
                    {
                        Addresses = MergeAddresses(candidate.Addresses, record.Addresses),
                        TimestampUtc = record.TimestampUtc < candidate.TimestampUtc
                            ? record.TimestampUtc
                            : candidate.TimestampUtc,
                        Status = record.Status,
                    };

                    entry.Record = merged;
                    entry.Version++;
                    ReplaceIndexAddresses(id, candidate.Addresses, merged.Addresses);
                    _dnsEviction.Enqueue((id, entry.Version), merged.TimestampUtc);
                    CompactDnsEviction();
                    return;
                }
            }

            var newId = ++_nextDnsId;
            var newEntry = new DnsEntry(record);
            _dns[newId] = newEntry;

            if (!_dnsByName.TryGetValue(nameKey, out ids))
            {
                ids = new List<long>(1);
                _dnsByName[nameKey] = ids;
            }

            ids.Add(newId);
            AddToIndex(newId, record.Addresses);
            _dnsEviction.Enqueue((newId, newEntry.Version), record.TimestampUtc);
            CompactDnsEviction();
        }
    }

    /// <summary>Returns a copy of the current connections.</summary>
    public IReadOnlyList<ConnectionRecord> GetConnectionSnapshot()
    {
        lock (_sync)
        {
            return _connections.Values.ToArray();
        }
    }

    /// <summary>Returns a copy of the current DNS records in no particular order.</summary>
    public IReadOnlyList<DnsRecord> GetDnsSnapshot()
    {
        lock (_sync)
        {
            return _dns.Values.Select(entry => entry.Record).ToArray();
        }
    }

    /// <summary>The number of DNS records currently stored.</summary>
    public int DnsCount
    {
        get
        {
            lock (_sync)
            {
                return _dns.Count;
            }
        }
    }

    /// <summary>
    /// Returns at most <paramref name="max"/> newest DNS records, newest first,
    /// using a bounded selection instead of copying and sorting the whole store.
    /// </summary>
    public IReadOnlyList<DnsRecord> GetNewestDns(int max)
    {
        if (max <= 0)
        {
            return Array.Empty<DnsRecord>();
        }

        lock (_sync)
        {
            if (_dns.Count <= max)
            {
                var all = _dns.Values.Select(entry => entry.Record).ToArray();
                Array.Sort(all, static (left, right) => right.TimestampUtc.CompareTo(left.TimestampUtc));
                return all;
            }

            var heap = new PriorityQueue<DnsRecord, DateTime>(max + 1);
            foreach (var entry in _dns.Values)
            {
                heap.Enqueue(entry.Record, entry.Record.TimestampUtc);
                if (heap.Count > max)
                {
                    heap.Dequeue();
                }
            }

            var newest = new DnsRecord[heap.Count];
            for (var i = 0; i < newest.Length; i++)
            {
                newest[i] = heap.Dequeue();
            }

            Array.Sort(newest, static (left, right) => right.TimestampUtc.CompareTo(left.TimestampUtc));
            return newest;
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

            var connectionsToRemove = _connections.Count - maxConnections;
            while (connectionsToRemove > 0 &&
                   _connectionEviction.TryDequeue(out var connectionItem, out var lastSeen))
            {
                if (!_connections.TryGetValue(connectionItem.Key, out var record) ||
                    !_connectionVersions.TryGetValue(connectionItem.Key, out var version) ||
                    version != connectionItem.Version ||
                    record.LastSeenUtc != lastSeen)
                {
                    continue;
                }

                _connections.Remove(connectionItem.Key);
                _connectionVersions.Remove(connectionItem.Key);
                evicted++;
                connectionsToRemove--;
            }

            var dnsToRemove = _dns.Count - maxDns;
            while (dnsToRemove > 0 && _dnsEviction.TryDequeue(out var dnsItem, out var timestamp))
            {
                if (!_dns.TryGetValue(dnsItem.Id, out var entry) ||
                    entry.Version != dnsItem.Version ||
                    entry.Record.TimestampUtc != timestamp)
                {
                    continue;
                }

                RemoveDns(dnsItem.Id, entry);
                evicted++;
                dnsToRemove--;
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
            _connectionVersions.Clear();
            _connectionEviction.Clear();
            _dns.Clear();
            _dnsByName.Clear();
            _dnsIndex.Clear();
            _dnsEviction.Clear();
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
        foreach (var id in candidates)
        {
            var record = _dns[id].Record;

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

    private static bool CanMerge(IPAddress[] left, IPAddress[] right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return left.Length == 0 && right.Length == 0;
        }

        foreach (var address in left)
        {
            foreach (var other in right)
            {
                if (address.Equals(other))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IPAddress[] MergeAddresses(IPAddress[] left, IPAddress[] right)
    {
        var merged = new List<IPAddress>(left.Length + right.Length);

        foreach (var address in left)
        {
            if (!merged.Contains(address))
            {
                merged.Add(address);
            }
        }

        foreach (var address in right)
        {
            if (!merged.Contains(address))
            {
                merged.Add(address);
            }
        }

        return merged.ToArray();
    }

    private int NextConnectionVersion(ConnectionIdentity key)
    {
        var version = _connectionVersions.TryGetValue(key, out var current) ? current + 1 : 1;
        _connectionVersions[key] = version;
        return version;
    }

    /// <summary>Bounds the lazy eviction queue when it outgrows the live set.</summary>
    private void CompactConnectionEviction()
    {
        if (_connectionEviction.Count <= _connections.Count * 4 + 8192)
        {
            return;
        }

        _connectionEviction.Clear();
        foreach (var (key, record) in _connections)
        {
            _connectionEviction.Enqueue((key, _connectionVersions[key]), record.LastSeenUtc);
        }
    }

    private void CompactDnsEviction()
    {
        if (_dnsEviction.Count <= _dns.Count * 4 + 8192)
        {
            return;
        }

        _dnsEviction.Clear();
        foreach (var (id, entry) in _dns)
        {
            _dnsEviction.Enqueue((id, entry.Version), entry.Record.TimestampUtc);
        }
    }

    private void ReplaceIndexAddresses(long id, IPAddress[] previous, IPAddress[] current)
    {
        foreach (var address in previous)
        {
            if (_dnsIndex.TryGetValue(address, out var ids))
            {
                ids.Remove(id);
                if (ids.Count == 0)
                {
                    _dnsIndex.Remove(address);
                }
            }
        }

        AddToIndex(id, current);
    }

    private void AddToIndex(long id, IPAddress[] addresses)
    {
        foreach (var address in addresses)
        {
            if (!_dnsIndex.TryGetValue(address, out var ids))
            {
                ids = new HashSet<long>();
                _dnsIndex[address] = ids;
            }

            ids.Add(id);
        }
    }

    private void RemoveDns(long id, DnsEntry entry)
    {
        var record = entry.Record;
        _dns.Remove(id);

        if (_dnsByName.TryGetValue((record.Process, record.QueryName), out var ids))
        {
            ids.Remove(id);
            if (ids.Count == 0)
            {
                _dnsByName.Remove((record.Process, record.QueryName));
            }
        }

        foreach (var address in record.Addresses)
        {
            if (_dnsIndex.TryGetValue(address, out var indexed))
            {
                indexed.Remove(id);
                if (indexed.Count == 0)
                {
                    _dnsIndex.Remove(address);
                }
            }
        }
    }

    private sealed class DnsEntry(DnsRecord record)
    {
        public DnsRecord Record { get; set; } = record;

        public int Version { get; set; }
    }
}
