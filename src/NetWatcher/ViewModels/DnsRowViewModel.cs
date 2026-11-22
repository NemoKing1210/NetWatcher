using System.Globalization;
using NetWatcher.Models;

namespace NetWatcher.ViewModels;

/// <summary>
/// One immutable DNS row. The key identifies the record instance, so the page
/// can reuse a row object for as long as the record has not changed.
/// </summary>
public sealed class DnsRowViewModel
{
    public DnsRowViewModel(DnsRecord record, string processName)
    {
        Record = record;
        Key = KeyOf(record);
        Time = record.TimestampUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        ProcessName = processName;
        Addresses = string.Join(", ", record.Addresses);
    }

    public string Key { get; }

    public DnsRecord Record { get; }

    public string Time { get; }

    public string ProcessName { get; }

    public int Pid => Record.Process.Pid;

    public string Domain => Record.QueryName;

    public string Addresses { get; }

    public string Status => Record.Status.ToString();

    /// <summary>Readable name for UI Automation, which uses ToString for row items.</summary>
    public override string ToString() => $"{Domain} {Addresses} {ProcessName}";

    /// <summary>The stable identity of a record, used to match rows across ticks.</summary>
    internal static string KeyOf(DnsRecord record) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{record.Process.Pid}|{record.Process.StartTimeUtc.Ticks}|{record.QueryName}|{record.TimestampUtc.Ticks}");
}
