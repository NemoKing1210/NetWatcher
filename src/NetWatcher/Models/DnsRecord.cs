using System.Net;

namespace NetWatcher.Models;

/// <summary>One DNS answer observed for a process, used to bind IPs to domains.</summary>
public sealed record DnsRecord(
    ProcessKey Process,
    string QueryName,
    IPAddress[] Addresses,
    DateTime TimestampUtc,
    DnsStatus Status);
