namespace NetWatcher.Models;

public enum Protocol
{
    Tcp,
    Udp,
}

public enum ConnectionSource
{
    Etw,
    Table,
}

public enum DnsStatus
{
    Success,
    NoResult,
    Error,
}

public enum DomainOrigin
{
    Dns,
    SharedCdn,
    Ptr,
    None,
}
