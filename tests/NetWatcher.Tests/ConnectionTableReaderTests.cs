using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetWatcher.Services.Connections;

namespace NetWatcher.Tests;

public sealed class ConnectionTableReaderTests
{
    [Fact]
    public void ParseTcpRow_ConvertsNetworkPortOrder()
    {
        var row = ConnectionTableReader.ParseTcpRow(
            pid: 1234,
            state: 5,
            remoteAddr: new byte[] { 93, 184, 216, 34 },
            remotePort: unchecked((ushort)0xBB01),
            localPort: unchecked((ushort)0x50C3),
            addressFamily: 2);

        Assert.NotNull(row);
        Assert.Equal(443, row.RemotePort);
        Assert.Equal(50000, row.LocalPort);
        Assert.Equal("93.184.216.34", row.RemoteAddress.ToString());
        Assert.Equal(TcpState.Established, row.State);
        Assert.False(row.Inbound);
    }

    [Fact]
    public void ParseTcpRow_Ipv6Address()
    {
        var address = new byte[]
        {
            0x20, 0x01, 0x0d, 0xb8, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0x01,
        };

        var row = ConnectionTableReader.ParseTcpRow(
            pid: 1234,
            state: 5,
            remoteAddr: address,
            remotePort: unchecked((ushort)0xBB01),
            localPort: unchecked((ushort)0x50C3),
            addressFamily: 23);

        Assert.NotNull(row);
        Assert.Equal("2001:db8::1", row.RemoteAddress.ToString());
        Assert.Equal(443, row.RemotePort);
    }

    [Fact]
    public void ParseTcpRow_NullForGarbage()
    {
        Assert.Null(ConnectionTableReader.ParseTcpRow(0, 5, new byte[4], 0, 0, 2));
        Assert.Null(ConnectionTableReader.ParseTcpRow(1234, 5, new byte[16], 0, 0, 99));
    }

    [Fact]
    public void ParseTcpRow_MarksListenerInbound()
    {
        var row = ConnectionTableReader.ParseTcpRow(
            pid: 1234,
            state: 2,
            remoteAddr: new byte[4],
            remotePort: NetworkOrder(0),
            localPort: NetworkOrder(443),
            addressFamily: 2);

        Assert.NotNull(row);
        Assert.True(row.Inbound);
    }

    [Fact]
    public void ParseTcpRow_MarksAcceptedConnectionInbound()
    {
        var row = ConnectionTableReader.ParseTcpRow(
            pid: 1234,
            state: 5,
            remoteAddr: new byte[] { 203, 0, 113, 9 },
            remotePort: NetworkOrder(51515),
            localPort: NetworkOrder(443),
            addressFamily: 2);

        Assert.NotNull(row);
        Assert.Equal(443, row.LocalPort);
        Assert.True(row.Inbound);
    }

    [Fact]
    public void ParseTcpRow_OutboundFromEphemeralPortIsNotInbound()
    {
        var row = ConnectionTableReader.ParseTcpRow(
            pid: 1234,
            state: 5,
            remoteAddr: new byte[] { 93, 184, 216, 34 },
            remotePort: NetworkOrder(443),
            localPort: NetworkOrder(51515),
            addressFamily: 2);

        Assert.NotNull(row);
        Assert.False(row.Inbound);
    }

    private static ushort NetworkOrder(int hostPort) =>
        unchecked((ushort)IPAddress.HostToNetworkOrder((short)hostPort));

    [Fact]
    [Trait("Category", "RequiresAdmin")]
    public void ReadTcpSnapshot_FindsOwnConnection()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var client = new TcpClient();
            client.Connect(IPAddress.Loopback, port);
            using var accepted = listener.AcceptTcpClient();

            var reader = new ConnectionTableReader(new ProcessResolver());
            var snapshot = reader.ReadTcpSnapshot(DateTime.UtcNow);

            Assert.Contains(snapshot, row =>
                row.Process.Pid == Environment.ProcessId &&
                row.RemoteAddress.Equals(IPAddress.Loopback) &&
                row.RemotePort == port);
        }
        finally
        {
            listener.Stop();
        }
    }
}
