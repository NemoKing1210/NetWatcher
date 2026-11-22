using NetWatcher.Models;
using NetWatcher.Services.Etw;

namespace NetWatcher.Tests;

public sealed class TcpUdpEventParserTests
{
    private static readonly ProcessKey Process = new(777, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    private static readonly DateTime Timestamp = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Normalize_MapsAllFields()
    {
        var result = TcpUdpEventParser.Normalize(
            Process,
            Timestamp,
            Protocol.Tcp,
            inbound: false,
            remoteAddress: new byte[] { 93, 184, 216, 34 },
            remotePort: 443,
            localPort: 50000,
            ConnectionSource.Etw);

        Assert.NotNull(result);
        Assert.Equal(Process, result.Process);
        Assert.Equal(Protocol.Tcp, result.Protocol);
        Assert.Equal("93.184.216.34", result.RemoteAddress.ToString());
        Assert.Equal(443, result.RemotePort);
        Assert.Equal(50000, result.LocalPort);
        Assert.False(result.Inbound);
        Assert.Equal(ConnectionSource.Etw, result.Source);
        Assert.Equal(Timestamp, result.TimestampUtc);
    }

    [Fact]
    public void Normalize_Ipv6Address()
    {
        var result = TcpUdpEventParser.Normalize(
            Process,
            Timestamp,
            Protocol.Udp,
            inbound: true,
            remoteAddress: new byte[]
            {
                0x20, 0x01, 0x0d, 0xb8, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0x01,
            },
            remotePort: 53,
            localPort: 50001,
            ConnectionSource.Etw);

        Assert.NotNull(result);
        Assert.Equal("2001:db8::1", result.RemoteAddress.ToString());
        Assert.Equal(Protocol.Udp, result.Protocol);
        Assert.True(result.Inbound);
    }

    [Fact]
    public void Normalize_InvalidAddressReturnsNull()
    {
        Assert.Null(TcpUdpEventParser.Normalize(
            Process,
            Timestamp,
            Protocol.Tcp,
            inbound: false,
            remoteAddress: new byte[5],
            remotePort: 443,
            localPort: 50000,
            ConnectionSource.Etw));
    }

    [Fact]
    public void Normalize_MapsIpv4MappedIpv6ToIpv4()
    {
        var result = TcpUdpEventParser.Normalize(
            Process,
            Timestamp,
            Protocol.Tcp,
            inbound: false,
            remoteAddress: new byte[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xff, 0xff, 1, 2, 3, 4,
            },
            remotePort: 443,
            localPort: 50000,
            ConnectionSource.Etw);

        Assert.NotNull(result);
        Assert.Equal("1.2.3.4", result.RemoteAddress.ToString());
        Assert.Equal(System.Net.Sockets.AddressFamily.InterNetwork, result.RemoteAddress.AddressFamily);
    }

    [Fact]
    public void DedupKey_EqualForSameTuple()
    {
        var first = MakeEvent(remotePort: 443, protocol: Protocol.Tcp);
        var second = MakeEvent(remotePort: 443, protocol: Protocol.Tcp);

        Assert.Equal(TcpUdpEventParser.DedupKey(first), TcpUdpEventParser.DedupKey(second));
    }

    [Fact]
    public void DedupKey_DiffersByPortAndProtocol()
    {
        var baseline = TcpUdpEventParser.DedupKey(MakeEvent(443, Protocol.Tcp));

        Assert.NotEqual(baseline, TcpUdpEventParser.DedupKey(MakeEvent(8443, Protocol.Tcp)));
        Assert.NotEqual(baseline, TcpUdpEventParser.DedupKey(MakeEvent(443, Protocol.Udp)));
    }

    private static ConnectionEvent MakeEvent(int remotePort, Protocol protocol) =>
        new(
            Process,
            protocol,
            System.Net.IPAddress.Parse("1.2.3.4"),
            remotePort,
            50000,
            Inbound: false,
            ConnectionSource.Etw,
            Timestamp);
}
