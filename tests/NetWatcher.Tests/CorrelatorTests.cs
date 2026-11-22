using System.Net;
using NetWatcher.Models;
using NetWatcher.Services.Correlation;
using NetWatcher.Storage;

namespace NetWatcher.Tests;

public sealed class CorrelatorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ProcessKey Process(int pid) => new(pid, T0.AddDays(-1));

    private static ConnectionRecord Connection(
        int pid,
        string remoteIp,
        DateTime firstSeen) =>
        new(
            Process(pid),
            Protocol.Tcp,
            IPAddress.Parse(remoteIp),
            443,
            50000,
            null,
            ConnectionSource.Etw,
            firstSeen,
            firstSeen,
            Inbound: false);

    private static DnsRecord Dns(
        int pid,
        string name,
        DateTime timestamp,
        params string[] ips) =>
        new(
            Process(pid),
            name,
            ips.Select(IPAddress.Parse).ToArray(),
            timestamp,
            DnsStatus.Success);

    [Fact]
    public void Bind_PidMatchWithinWindow()
    {
        var store = new NetworkStore();
        store.AddDns(Dns(100, "example.com", T0.AddSeconds(-1), "1.2.3.4"));
        var correlator = new DomainCorrelator(store);

        var binding = correlator.Bind(Connection(100, "1.2.3.4", T0), T0);

        Assert.Equal("example.com", binding.Domain);
        Assert.Equal(DomainOrigin.Dns, binding.Origin);
    }

    [Fact]
    public void Bind_PrefersNearestInTime()
    {
        var store = new NetworkStore();
        store.AddDns(Dns(100, "old.example", T0.AddSeconds(-30), "1.2.3.4"));
        store.AddDns(Dns(100, "new.example", T0.AddSeconds(-1), "1.2.3.4"));
        var correlator = new DomainCorrelator(store);

        var binding = correlator.Bind(Connection(100, "1.2.3.4", T0), T0);

        Assert.Equal("new.example", binding.Domain);
    }

    [Fact]
    public void Bind_MultiIpAnswer()
    {
        var store = new NetworkStore();
        store.AddDns(Dns(100, "cdn.example", T0.AddSeconds(-1), "1.2.3.4", "5.6.7.8"));
        var correlator = new DomainCorrelator(store);

        var binding = correlator.Bind(Connection(100, "5.6.7.8", T0), T0);

        Assert.Equal("cdn.example", binding.Domain);
        Assert.Equal(DomainOrigin.Dns, binding.Origin);
    }

    [Fact]
    public void Bind_FallsBackToGlobalAsSharedCdn()
    {
        var store = new NetworkStore();
        store.AddDns(Dns(200, "other.example", T0.AddSeconds(-30), "1.2.3.4"));
        var correlator = new DomainCorrelator(store);

        var binding = correlator.Bind(Connection(100, "1.2.3.4", T0), T0);

        Assert.Equal("other.example", binding.Domain);
        Assert.Equal(DomainOrigin.SharedCdn, binding.Origin);
    }

    [Fact]
    public void Bind_OutsideWindowsReturnsNone()
    {
        var store = new NetworkStore();
        store.AddDns(Dns(100, "stale.example", T0.AddHours(-1), "1.2.3.4"));
        var correlator = new DomainCorrelator(store);

        var binding = correlator.Bind(Connection(100, "1.2.3.4", T0), T0);

        Assert.Null(binding.Domain);
        Assert.Equal(DomainOrigin.None, binding.Origin);
    }

    [Fact]
    public void Bind_IgnoresOtherPidOutsideGlobalWindow()
    {
        var store = new NetworkStore();
        store.AddDns(Dns(200, "other.example", T0.AddMinutes(-10), "1.2.3.4"));
        var correlator = new DomainCorrelator(store);

        var binding = correlator.Bind(Connection(100, "1.2.3.4", T0), T0);

        Assert.Equal(DomainOrigin.None, binding.Origin);
    }

    [Fact]
    public void Bind_LateSameProcessAnswerStaysDnsNotShared()
    {
        var store = new NetworkStore();
        store.AddDns(Dns(100, "late.example", T0.AddMinutes(1), "1.2.3.4"));
        var correlator = new DomainCorrelator(store);

        var binding = correlator.Bind(Connection(100, "1.2.3.4", T0), T0);

        Assert.Equal("late.example", binding.Domain);
        Assert.Equal(DomainOrigin.Dns, binding.Origin);
    }
}
