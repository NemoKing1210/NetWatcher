using System.Net;
using System.Net.NetworkInformation;

namespace NetWatcher.Models;

/// <summary>A normalized connection observation produced by ETW or a table snapshot.</summary>
public sealed record ConnectionEvent(
    ProcessKey Process,
    Protocol Protocol,
    IPAddress RemoteAddress,
    int RemotePort,
    int LocalPort,
    bool Inbound,
    ConnectionSource Source,
    DateTime TimestampUtc);

/// <summary>A connection as stored for the session, with first/last seen timestamps.</summary>
public sealed record ConnectionRecord(
    ProcessKey Process,
    Protocol Protocol,
    IPAddress RemoteAddress,
    int RemotePort,
    int LocalPort,
    TcpState? State,
    ConnectionSource Source,
    DateTime FirstSeenUtc,
    DateTime LastSeenUtc,
    bool Inbound);

/// <summary>The domain decided for a connection, and where that domain came from.</summary>
public sealed record DomainBinding(string? Domain, DomainOrigin Origin);
