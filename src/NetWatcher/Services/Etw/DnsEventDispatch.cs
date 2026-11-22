namespace NetWatcher.Services.Etw;

/// <summary>How a DNS-Client event should be handled, decided from its payload.</summary>
public enum DnsEventKind
{
    /// <summary>Carries a query name and an answer string; parse it.</summary>
    Completed,

    /// <summary>A recognised DNS event without answers (query started, etc.); ignore it.</summary>
    Recognized,

    /// <summary>Carries neither a query name nor an answer; report it once.</summary>
    Unknown,
}

/// <summary>
/// Decides what a DNS-Client event is from the fields it carries rather than
/// from a hard-coded event-ID list. Windows builds disagree about the IDs —
/// Windows 11 emits 3009-3020 where older builds emit 3006/3008 — but the
/// payload shape of a completed query is stable.
/// </summary>
public static class DnsEventDispatch
{
    public static DnsEventKind Classify(string? queryName, string? queryResults)
    {
        var hasName = !string.IsNullOrWhiteSpace(queryName);
        var hasResults = !string.IsNullOrWhiteSpace(queryResults);

        if (hasName && hasResults)
        {
            return DnsEventKind.Completed;
        }

        return hasName ? DnsEventKind.Recognized : DnsEventKind.Unknown;
    }
}

/// <summary>Remembers event IDs already logged, so one unknown ID is reported once.</summary>
public sealed class SeenEventIds
{
    private readonly HashSet<int> _seen = new();

    public bool TryAdd(int eventId) => _seen.Add(eventId);
}
