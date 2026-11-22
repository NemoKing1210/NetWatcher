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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stable identity of the connection this row renders.</summary>
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

    /// <summary>First-seen time as ISO-8601 UTC for exports.</summary>
    public string TimeIso => _record.FirstSeenUtc.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>Last-activity time as ISO-8601 UTC for exports.</summary>
    public string LastActivityIso => _record.LastSeenUtc.ToString("O", CultureInfo.InvariantCulture);

    public bool Inbound => _record.Inbound;

    public bool IsPtr => _binding.Origin == DomainOrigin.Ptr;

    public bool IsShared => _binding.Origin == DomainOrigin.SharedCdn;

    public bool ShowGrayed => _binding.Origin == DomainOrigin.None;

    /// <summary>The owning executable's icon, loaded lazily for the table's Process cell.</summary>
    public ProcessIcon Icon { get; } = new();

    /// <summary>Readable name for UI Automation, which uses ToString for row items.</summary>
    public override string ToString() =>
        $"{_processName} {DomainDisplay} {Ip}:{Port} {State}";

    /// <summary>Refreshes the row in place, notifying only the values that changed.</summary>
    internal void Update(
        ConnectionRecord record,
        DomainBinding binding,
        string processName,
        string? processPath)
    {
        Icon.SetPath(processPath);

        if (_record == record && _binding == binding && _processName == processName)
        {
            return;
        }

        var previousRecord = _record;
        var previousBinding = _binding;
        var firstSeenChanged = previousRecord.FirstSeenUtc != record.FirstSeenUtc;
        var lastSeenChanged = previousRecord.LastSeenUtc != record.LastSeenUtc;
        var stateChanged = previousRecord.State != record.State;
        var sourceChanged = previousRecord.Source != record.Source;
        var bindingChanged = previousBinding != binding;
        var nameChanged = _processName != processName;
        var recordChanged = previousRecord != record;

        _record = record;
        _binding = binding;
        _processName = processName;

        if (firstSeenChanged)
        {
            OnPropertyChanged(nameof(Time));
        }

        if (bindingChanged)
        {
            OnPropertyChanged(nameof(DomainDisplay));
            OnPropertyChanged(nameof(IsPtr));
            OnPropertyChanged(nameof(IsShared));
            OnPropertyChanged(nameof(ShowGrayed));
            OnPropertyChanged(nameof(Binding));
        }

        if (stateChanged)
        {
            OnPropertyChanged(nameof(State));
        }

        if (lastSeenChanged)
        {
            OnPropertyChanged(nameof(LastActivity));
        }

        if (sourceChanged)
        {
            OnPropertyChanged(nameof(SourceName));
        }

        if (nameChanged)
        {
            OnPropertyChanged(nameof(ProcessName));
        }

        if (recordChanged)
        {
            OnPropertyChanged(nameof(Record));
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string Format(DateTime utc) =>
        utc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
}
