using System.ComponentModel;
using System.Globalization;
using NetWatcher.Models;

namespace NetWatcher.ViewModels;

/// <summary>
/// One row of the connection table: a stored connection plus its domain binding.
/// Instances are reused across refreshes so the user's sort, selection, and
/// scroll position survive the 1 Hz update.
/// </summary>
public sealed class ConnectionRowViewModel : INotifyPropertyChanged
{
    private ConnectionRecord _record;
    private DomainBinding _binding;
    private string _processName;

    public ConnectionRowViewModel(
        ConnectionRecord record,
        DomainBinding binding,
        string? processName = null)
    {
        _record = record;
        _binding = binding;
        _processName = processName ?? $"pid:{record.Process.Pid}";
        Key = KeyOf(record);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stable identity of the connection this row renders.</summary>
    public string Key { get; }

    public ConnectionRecord Record => _record;

    public DomainBinding Binding => _binding;

    public string ProcessName => _processName;

    public string Time => Format(_record.FirstSeenUtc);

    public int Pid => _record.Process.Pid;

    /// <summary>"domain", "~domain" for shared/CDN, the PTR host, or "—".</summary>
    public string DomainDisplay => _binding.Origin switch
    {
        DomainOrigin.Dns => _binding.Domain ?? "—",
        DomainOrigin.SharedCdn => "~" + (_binding.Domain ?? string.Empty),
        DomainOrigin.Ptr => _binding.Domain ?? "—",
        _ => "—",
    };

    public string Ip => _record.RemoteAddress.ToString();

    public int Port => _record.RemotePort;

    public string ProtocolName => _record.Protocol == Models.Protocol.Tcp ? "TCP" : "UDP";

    public string State => _record.State?.ToString() ?? string.Empty;

    public string SourceName => _record.Source == ConnectionSource.Etw ? "ETW" : "Table";

    public string LastActivity => Format(_record.LastSeenUtc);

    public bool Inbound => _record.Inbound;

    public bool IsPtr => _binding.Origin == DomainOrigin.Ptr;

    public bool IsShared => _binding.Origin == DomainOrigin.SharedCdn;

    public bool ShowGrayed => _binding.Origin == DomainOrigin.None;

    /// <summary>Readable name for UI Automation, which uses ToString for row items.</summary>
    public override string ToString() =>
        $"{_processName} {DomainDisplay} {Ip}:{Port} {State}";

    /// <summary>Identity of the connection a record describes, shared with the store's key.</summary>
    public static string KeyOf(ConnectionRecord record) =>
        $"{record.Process.Pid}|{record.Process.StartTimeUtc.Ticks}|{(int)record.Protocol}|" +
        $"{record.RemoteAddress}|{record.RemotePort}|{record.LocalPort}|{record.Inbound}";

    /// <summary>Refreshes the row in place, notifying only the values that changed.</summary>
    internal void Update(ConnectionRecord record, DomainBinding binding, string processName)
    {
        var time = Time;
        var domain = DomainDisplay;
        var ip = Ip;
        var port = Port;
        var protocol = ProtocolName;
        var state = State;
        var source = SourceName;
        var lastActivity = LastActivity;
        var pid = Pid;
        var process = _processName;
        var ptr = IsPtr;
        var shared = IsShared;
        var grayed = ShowGrayed;
        var inbound = Inbound;
        var previousRecord = _record;
        var previousBinding = _binding;

        _record = record;
        _binding = binding;
        _processName = processName;

        NotifyIfChanged(time, Time, nameof(Time));
        NotifyIfChanged(domain, DomainDisplay, nameof(DomainDisplay));
        NotifyIfChanged(ip, Ip, nameof(Ip));
        NotifyIfChanged(port, Port, nameof(Port));
        NotifyIfChanged(protocol, ProtocolName, nameof(ProtocolName));
        NotifyIfChanged(state, State, nameof(State));
        NotifyIfChanged(source, SourceName, nameof(SourceName));
        NotifyIfChanged(lastActivity, LastActivity, nameof(LastActivity));
        NotifyIfChanged(pid, Pid, nameof(Pid));
        NotifyIfChanged(process, _processName, nameof(ProcessName));
        NotifyIfChanged(ptr, IsPtr, nameof(IsPtr));
        NotifyIfChanged(shared, IsShared, nameof(IsShared));
        NotifyIfChanged(grayed, ShowGrayed, nameof(ShowGrayed));
        NotifyIfChanged(inbound, Inbound, nameof(Inbound));
        NotifyIfChanged(previousRecord, _record, nameof(Record));
        NotifyIfChanged(previousBinding, _binding, nameof(Binding));
    }

    private void NotifyIfChanged<T>(T oldValue, T newValue, string propertyName)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string Format(DateTime utc) =>
        utc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
}
