using System.ComponentModel;

namespace NetWatcher.ViewModels;

/// <summary>
/// One process with its current connection count. Instances are reused per
/// process so the process select's selection survives refreshes.
/// </summary>
public sealed class ProcessItemViewModel : INotifyPropertyChanged
{
    private string _name;
    private string? _path;
    private int _connectionCount;
    private DateTime _lastActivityUtc;

    public ProcessItemViewModel(
        int pid,
        string name,
        string? path,
        int connectionCount,
        DateTime lastActivityUtc)
    {
        Pid = pid;
        _name = name;
        _path = path;
        _connectionCount = connectionCount;
        _lastActivityUtc = lastActivityUtc;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Pid { get; }

    public string Name => _name;

    /// <summary>The executable path, when Windows lets us read it; the icon source.</summary>
    public string? Path => _path;

    public int ConnectionCount => _connectionCount;

    public DateTime LastActivityUtc => _lastActivityUtc;

    public string Display => $"{_name} ({Pid}), {_connectionCount} connections";

    /// <summary>Readable name for UI Automation.</summary>
    public override string ToString() => Display;

    /// <summary>Refreshes the entry in place, notifying only the values that changed.</summary>
    internal void Update(string name, string? path, int connectionCount, DateTime lastActivityUtc)
    {
        var displayChanged = false;

        if (_name != name)
        {
            _name = name;
            displayChanged = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }

        if (!string.Equals(_path, path, StringComparison.OrdinalIgnoreCase))
        {
            _path = path;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Path)));
        }
        if (_connectionCount != connectionCount)
        {
            _connectionCount = connectionCount;
            displayChanged = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectionCount)));
        }

        if (_lastActivityUtc != lastActivityUtc)
        {
            _lastActivityUtc = lastActivityUtc;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastActivityUtc)));
        }

        if (displayChanged)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
        }
    }
}
