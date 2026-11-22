using NetWatcher.Models;
using NetWatcher.Storage;

namespace NetWatcher.Services.Correlation;

/// <summary>
/// Binds a connection's remote IP to a domain using the spec's algorithm:
/// process-scoped DNS within −15 min … +2 s of the connection, then a global
/// ±2 min window marked as shared/CDN, then nothing (PTR is the caller's job).
/// </summary>
public sealed class DomainCorrelator(NetworkStore store)
{
    private static readonly TimeSpan PidLookback = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PidLookahead = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan GlobalWindow = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Returns the domain to display for <paramref name="connection"/>.
    /// <paramref name="nowUtc"/> is the observation time used by callers; the
    /// windows themselves are anchored to the connection's first-seen time.
    /// </summary>
    public DomainBinding Bind(ConnectionRecord connection, DateTime nowUtc)
    {
        var own = store.FindBestDns(
            connection.Process,
            connection.RemoteAddress,
            connection.FirstSeenUtc - PidLookback,
            connection.FirstSeenUtc + PidLookahead);

        if (own is not null)
        {
            return new DomainBinding(own.QueryName, DomainOrigin.Dns);
        }

        var shared = store.FindBestDnsGlobal(
            connection.RemoteAddress,
            connection.FirstSeenUtc - GlobalWindow,
            connection.FirstSeenUtc + GlobalWindow);

        if (shared is not null)
        {
            return new DomainBinding(shared.QueryName, DomainOrigin.SharedCdn);
        }

        return new DomainBinding(null, DomainOrigin.None);
    }
}
