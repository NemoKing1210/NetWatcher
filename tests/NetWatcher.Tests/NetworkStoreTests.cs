using System.Net;
using System.Net.NetworkInformation;
using NetWatcher.Models;
using NetWatcher.Storage;

namespace NetWatcher.Tests;

public sealed class NetworkStoreTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ProcessKey Process(int pid = 100) => new(pid, T0.AddDays(-1));

    private static ConnectionRecord Connection(
        string ip,
        int remotePort,
        DateTime firstSeen,
        DateTime lastSeen,
        int pid = 100,
        TcpState? state = null) =>
        new(
            Process(pid),
            Protocol.Tcp,
            IPAddress.Parse(ip),
            remotePort,
            50000 + remotePort % 1000,
            state ?? TcpState.SynSent,
            ConnectionSource.Etw,
            firstSeen,
            lastSeen,
            Inbound: false);

    private static DnsRecord Dns(
        string name,
        string ip,
        DateTime timestamp,
        int pid = 100) =>
        new(Process(pid), name, new[] { IPAddress.Parse(ip) }, timestamp, DnsStatus.Success);

    [Fact]
    public void UpsertConnection_KeepsFirstSeenUpdatesLastSeen()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection("1.2.3.4", 443, T0, T0));
        store.UpsertConnection(Connection("1.2.3.4", 443, T0, T0.AddSeconds(5), state: TcpState.Established));

        var row = Assert.Single(store.GetConnectionSnapshot());
        Assert.Equal(T0, row.FirstSeenUtc);
        Assert.Equal(T0.AddSeconds(5), row.LastSeenUtc);
        Assert.Equal(TcpState.Established, row.State);
    }

    [Fact]
    public void AddDns_DedupsWithin30Seconds()
    {
        var store = new NetworkStore();
        store.AddDns(Dns("example.com", "1.2.3.4", T0));
        store.AddDns(Dns("example.com", "1.2.3.4", T0.AddSeconds(1)));

        Assert.Single(store.GetDnsSnapshot());
    }

    [Fact]
    public void FindBestDns_PidScopedWindowFilter()
    {
        var store = new NetworkStore();
        store.AddDns(Dns("example.com", "1.2.3.4", T0));

        Assert.NotNull(store.FindBestDns(Process(), IPAddress.Parse("1.2.3.4"), T0.AddMinutes(-15), T0.AddSeconds(2)));
        Assert.Null(store.FindBestDns(Process(), IPAddress.Parse("1.2.3.4"), T0.AddSeconds(3), T0.AddSeconds(10)));
    }

    [Fact]
    public void FindBestDns_NearestTimeWins()
    {
        var store = new NetworkStore();
        store.AddDns(Dns("old.example", "1.2.3.4", T0.AddSeconds(-10)));
        store.AddDns(Dns("new.example", "1.2.3.4", T0.AddSeconds(-1)));

        var best = store.FindBestDns(
            Process(),
            IPAddress.Parse("1.2.3.4"),
            T0.AddMinutes(-15),
            T0.AddSeconds(2));

        Assert.NotNull(best);
        Assert.Equal("new.example", best.QueryName);
    }

    [Fact]
    public void FindBestDnsGlobal_IgnoresPid()
    {
        var store = new NetworkStore();
        store.AddDns(Dns("other.example", "1.2.3.4", T0, pid: 200));

        Assert.Null(store.FindBestDns(Process(), IPAddress.Parse("1.2.3.4"), T0.AddMinutes(-15), T0.AddMinutes(15)));
        Assert.NotNull(store.FindBestDnsGlobal(IPAddress.Parse("1.2.3.4"), T0.AddMinutes(-2), T0.AddMinutes(2)));
    }

    [Fact]
    public void EvictByLastSeen_CapsAtLimit()
    {
        var store = new NetworkStore();
        for (var i = 0; i < 100; i++)
        {
            store.UpsertConnection(Connection("1.2.3.4", 1000 + i, T0, T0.AddSeconds(i)));
        }

        var evicted = store.EvictByLastSeen(maxConnections: 50, maxDns: 0);

        Assert.Equal(50, evicted);
        Assert.Equal(50, store.GetConnectionSnapshot().Count);
        Assert.DoesNotContain(store.GetConnectionSnapshot(), r => r.RemotePort == 1000);
    }

    [Fact]
    public void ConcurrentUpserts_DoNotCorrupt()
    {
        var store = new NetworkStore();

        Parallel.For(0, 1000, i =>
            store.UpsertConnection(Connection("1.2.3.4", 10000 + i, T0, T0)));

        Assert.Equal(1000, store.GetConnectionSnapshot().Count);
    }
}
