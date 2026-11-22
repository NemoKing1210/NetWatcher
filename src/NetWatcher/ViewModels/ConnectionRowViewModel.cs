using System.Globalization;
using NetWatcher.Models;

namespace NetWatcher.ViewModels;

/// <summary>One row of the connection table: a stored connection plus its domain binding.</summary>
public sealed class ConnectionRowViewModel
{
    public ConnectionRowViewModel(
        ConnectionRecord record,
        DomainBinding binding,
        string? processName = null)
    {
        Record = record;
        Binding = binding;
        ProcessName = processName ?? $"pid:{record.Process.Pid}";
    }

    public ConnectionRecord Record { get; }

    public DomainBinding Binding { get; }

    public string ProcessName { get; }

    public string Time => Format(Record.FirstSeenUtc);

    public int Pid => Record.Process.Pid;

    /// <summary>"domain", "~domain" for shared/CDN, the PTR host, or "—".</summary>
    public string DomainDisplay => Binding.Origin switch
    {
        DomainOrigin.Dns => Binding.Domain ?? "—",
        DomainOrigin.SharedCdn => "~" + (Binding.Domain ?? string.Empty),
        DomainOrigin.Ptr => Binding.Domain ?? "—",
        _ => "—",
    };

    public string Ip => Record.RemoteAddress.ToString();

    public int Port => Record.RemotePort;

    public string ProtocolName => Record.Protocol == Models.Protocol.Tcp ? "TCP" : "UDP";

    public string State => Record.State?.ToString() ?? string.Empty;

    public string SourceName => Record.Source == ConnectionSource.Etw ? "ETW" : "TABLE";

    public string LastActivity => Format(Record.LastSeenUtc);

    public bool Inbound => Record.Inbound;

    public bool IsNew => DateTime.UtcNow - Record.FirstSeenUtc < TimeSpan.FromSeconds(10);

    public bool IsPtr => Binding.Origin == DomainOrigin.Ptr;

    public bool IsShared => Binding.Origin == DomainOrigin.SharedCdn;

    public bool ShowGrayed => Binding.Origin == DomainOrigin.None;

    private static string Format(DateTime utc) =>
        utc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
}
