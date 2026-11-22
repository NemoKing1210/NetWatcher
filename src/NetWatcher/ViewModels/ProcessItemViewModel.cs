using System.ComponentModel;

namespace NetWatcher.ViewModels;

/// <summary>
/// One entry in the process list with its current connection count. Instances
/// are reused per process so a navigation-pane selection survives refreshes.
/// </summary>
public sealed class ProcessItemViewModel : INotifyPropertyChanged
{
    private string _name;
    private int _connectionCount;
    private DateTime _lastActivityUtc;

    public ProcessItemViewModel(int pid, string name, int connectionCount, DateTime lastActivityUtc)
    {
        Pid = pid;
        _name = name;
        _connectionCount = connectionCount;
        _lastActivityUtc = lastActivityUtc;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Pid { get; }

    public string Name => _name;

    public int ConnectionCount => _connectionCount;

    public DateTime LastActivityUtc => _lastActivityUtc;

    public string Display => $"{_name} ({Pid}) · {_connectionCount}";

    /// <summary>Refreshes the entry in place, notifying only the values that changed.</summary>
    internal void Update(string name, int connectionCount, DateTime lastActivityUtc)
    {
        var displayChanged = false;

        if (_name != name)
        {
            _name = name;
            displayChanged = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
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
