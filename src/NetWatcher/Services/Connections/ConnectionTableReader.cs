using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using NetWatcher.Models;
using NetWatcher.Utils;

namespace NetWatcher.Services.Connections;

/// <summary>
/// Reads the Windows TCP connection table (IPv4 and IPv6) through
/// <c>GetExtendedTcpTable</c>. This is the fallback when ETW is unavailable and
/// the source of connection state while ETW is live.
/// </summary>
public sealed class ConnectionTableReader(ProcessResolver resolver)
{
    private const int V4RowSize = 24;
    private const int V6RowSize = 56;

    /// <summary>Returns one record per TCP row, with the owning process resolved.</summary>
    public IReadOnlyList<ConnectionRecord> ReadTcpSnapshot(DateTime timestampUtc)
    {
        var results = new List<ConnectionRecord>();
        ReadTable(NativeMethods.AfInet, V4RowSize, timestampUtc, results);
        ReadTable(NativeMethods.AfInet6, V6RowSize, timestampUtc, results);
        return results;
    }

    /// <summary>
    /// Converts one raw table row into a record. Ports are expected in network
    /// byte order, as the native tables store them.
    /// </summary>
    internal static ConnectionRecord? ParseTcpRow(
        int pid,
        int state,
        byte[] remoteAddr,
        ushort remotePort,
        ushort localPort,
        int addressFamily)
    {
        if (pid <= 0)
        {
            return null;
        }

        if (addressFamily is not (NativeMethods.AfInet or NativeMethods.AfInet6))
        {
            return null;
        }

        IPAddress remote;
        try
        {
            remote = IpHelpers.ParseAddressBytes(
                remoteAddr,
                0,
                addressFamily == NativeMethods.AfInet ? 4 : 16);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var inbound = IsInbound(remote, localPort, remotePort);

        return new ConnectionRecord(
            new ProcessKey(pid, DateTime.MinValue),
            Protocol.Tcp,
            remote,
            IpHelpers.NetworkPortToHost(remotePort),
            IpHelpers.NetworkPortToHost(localPort),
            MapState(state),
            ConnectionSource.Table,
            DateTime.MinValue,
            DateTime.MinValue,
            inbound);
    }

    // A listening socket has no remote address. An accepted connection arrives
    // on the service port while the peer uses an ephemeral one.
    private static bool IsInbound(IPAddress remote, ushort localPort, ushort remotePort)
    {
        if (remote.Equals(IPAddress.Any) || remote.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        return !IsEphemeral(IpHelpers.NetworkPortToHost(localPort)) &&
               IsEphemeral(IpHelpers.NetworkPortToHost(remotePort));
    }

    // Windows picks client ports from 49152-65535 (IANA dynamic range).
    private static bool IsEphemeral(ushort port) => port >= 49152;

    private void ReadTable(
        int addressFamily,
        int rowSize,
        DateTime timestampUtc,
        List<ConnectionRecord> results)
    {
        var size = 0;
        NativeMethods.GetExtendedTcpTable(
            IntPtr.Zero,
            ref size,
            false,
            addressFamily,
            NativeMethods.TcpTableOwnerPidConnections,
            0);

        if (size <= 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var result = NativeMethods.GetExtendedTcpTable(
                buffer,
                ref size,
                false,
                addressFamily,
                NativeMethods.TcpTableOwnerPidConnections,
                0);

            if (result != 0)
            {
                return;
            }

            var bytes = new byte[size];
            Marshal.Copy(buffer, bytes, 0, size);

            var count = BitConverter.ToInt32(bytes, 0);
            var offset = 4;
            for (var i = 0; i < count && offset + rowSize <= bytes.Length; i++, offset += rowSize)
            {
                var row = addressFamily == NativeMethods.AfInet
                    ? ParseV4Row(bytes, offset)
                    : ParseV6Row(bytes, offset);

                if (row is null)
                {
                    continue;
                }

                var info = resolver.GetOrAdd(row.Process.Pid);
                results.Add(row with
                {
                    Process = info.Key,
                    FirstSeenUtc = timestampUtc,
                    LastSeenUtc = timestampUtc,
                });
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static ConnectionRecord? ParseV4Row(byte[] bytes, int offset)
    {
        var state = BitConverter.ToInt32(bytes, offset);
        var localPort = ReadPort(bytes, offset + 8);
        var remoteAddr = new byte[4];
        Array.Copy(bytes, offset + 12, remoteAddr, 0, 4);
        var remotePort = ReadPort(bytes, offset + 16);
        var pid = BitConverter.ToInt32(bytes, offset + 20);

        return ParseTcpRow(pid, state, remoteAddr, remotePort, localPort, NativeMethods.AfInet);
    }

    private static ConnectionRecord? ParseV6Row(byte[] bytes, int offset)
    {
        var localPort = ReadPort(bytes, offset + 20);
        var remoteAddr = new byte[16];
        Array.Copy(bytes, offset + 24, remoteAddr, 0, 16);
        var remotePort = ReadPort(bytes, offset + 44);
        var state = BitConverter.ToInt32(bytes, offset + 48);
        var pid = BitConverter.ToInt32(bytes, offset + 52);

        return ParseTcpRow(pid, state, remoteAddr, remotePort, localPort, NativeMethods.AfInet6);
    }

    // The port occupies the low two bytes of the network-order DWORD; reading
    // them as little-endian yields the byte-swapped value that
    // IpHelpers.NetworkPortToHost expects.
    private static ushort ReadPort(byte[] bytes, int portDwordOffset) =>
        BitConverter.ToUInt16(bytes, portDwordOffset);

    private static TcpState MapState(int state) => state switch
    {
        1 => TcpState.Closed,
        2 => TcpState.Listen,
        3 => TcpState.SynSent,
        4 => TcpState.SynReceived,
        5 => TcpState.Established,
        6 => TcpState.FinWait1,
        7 => TcpState.FinWait2,
        8 => TcpState.CloseWait,
        9 => TcpState.Closing,
        10 => TcpState.LastAck,
        11 => TcpState.TimeWait,
        12 => TcpState.DeleteTcb,
        _ => TcpState.Unknown,
    };
}
