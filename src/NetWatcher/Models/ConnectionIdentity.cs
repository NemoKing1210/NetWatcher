using System.Net;

namespace NetWatcher.Models;

/// <summary>
/// Stable identity of one connection across ETW and table observations.
/// Ports are in host order and <see cref="RemoteAddress"/> is normalized to IPv4
/// when it arrived as an IPv4-mapped IPv6 address.
/// </summary>
public readonly record struct ConnectionIdentity(
    ProcessKey Process,
    Protocol Protocol,
    IPAddress RemoteAddress,
    int RemotePort,
    int LocalPort,
    bool Inbound)
{
    public static ConnectionIdentity From(ConnectionRecord record) => new(
        record.Process,
        record.Protocol,
        record.RemoteAddress,
        record.RemotePort,
        record.LocalPort,
        record.Inbound);
}
