using System.Net;
using NetWatcher.Models;
using NetWatcher.Utils;

namespace NetWatcher.Services.Etw;

/// <summary>
/// Normalizes raw Kernel-Network ETW fields into <see cref="ConnectionEvent"/>s
/// and derives the key used to collapse duplicate observations.
/// </summary>
public static class TcpUdpEventParser
{
    /// <summary>
    /// Builds a connection event, or null when the address or port is invalid
    /// (the caller logs and skips it).
    /// </summary>
    public static ConnectionEvent? Normalize(
        ProcessKey process,
        DateTime timestampUtc,
        Protocol protocol,
        bool inbound,
        byte[] remoteAddress,
        int remotePort,
        int localPort,
        ConnectionSource source)
    {
        if (remoteAddress is null)
        {
            return null;
        }

        if (remotePort is < 0 or > 65535 || localPort is < 0 or > 65535)
        {
            return null;
        }

        IPAddress remote;
        try
        {
            remote = IpHelpers.ParseAddressBytes(remoteAddress, 0, remoteAddress.Length);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (remote.IsIPv4MappedToIPv6)
        {
            remote = remote.MapToIPv4();
        }

        return new ConnectionEvent(
            process,
            protocol,
            remote,
            remotePort,
            localPort,
            inbound,
            source,
            timestampUtc);
    }

    /// <summary>
    /// Key that identifies one connection across ConnectionAttempted/Connect
    /// duplicates and across table/ETW sources.
    /// </summary>
    public static string DedupKey(ConnectionEvent connection) =>
        $"{connection.Process.Pid}|{(int)connection.Protocol}|{connection.RemoteAddress}:{connection.RemotePort}|{connection.LocalPort}";
}
