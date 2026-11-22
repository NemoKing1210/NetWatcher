using System.Net;
using System.Net.Sockets;
using NetWatcher.Models;

namespace NetWatcher.Services.Etw;

/// <summary>
/// Parses the DNS-Client provider's QueryResults payload into IP addresses.
/// The provider reports A/AAAA answers; anything that does not resolve to an
/// IPv4 or IPv6 address is discarded rather than throwing.
/// </summary>
public static class DnsEventParser
{
    private static readonly char[] EntrySeparators = { ';', ',', '\r', '\n', ' ', '\t' };

    /// <summary>
    /// Builds a DNS record from an event, or null when the answer contains no
    /// usable address (non-A/AAAA answers, failures, garbage).
    /// </summary>
    public static DnsRecord? Parse(
        ProcessKey process,
        DateTime timestampUtc,
        string queryName,
        DnsStatus status,
        string queryResults)
    {
        var addresses = ParseQueryResults(queryResults);
        if (addresses.Length == 0)
        {
            return null;
        }

        return new DnsRecord(process, queryName, addresses, timestampUtc, status);
    }

    /// <summary>
    /// Extracts addresses from the textual QueryResults form emitted by the
    /// provider (entries such as <c>type: 1 1.2.3.4;</c> or bare addresses).
    /// </summary>
    public static IPAddress[] ParseQueryResults(string queryResults)
    {
        if (string.IsNullOrWhiteSpace(queryResults))
        {
            return Array.Empty<IPAddress>();
        }

        var results = new List<IPAddress>();
        foreach (var token in queryResults.Split(EntrySeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseAddress(token, out var address) && !results.Contains(address))
            {
                results.Add(address);
            }
        }

        return results.ToArray();
    }

    private static bool TryParseAddress(string token, out IPAddress address)
    {
        address = IPAddress.None;

        var candidate = token.Trim().TrimEnd('.', ',', ';', ')', '"');

        // Require a full dotted quad for IPv4; IPAddress.TryParse alone would
        // accept partial forms such as "1" or "1.2".
        if (candidate.Count(c => c == '.') == 3 &&
            IPAddress.TryParse(candidate, out var v4) &&
            v4.AddressFamily == AddressFamily.InterNetwork)
        {
            address = v4;
            return true;
        }

        if (candidate.Contains(':') &&
            IPAddress.TryParse(candidate, out var v6) &&
            v6.AddressFamily == AddressFamily.InterNetworkV6)
        {
            address = v6;
            return true;
        }

        return false;
    }
}
