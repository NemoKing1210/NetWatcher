namespace NetWatcher.ViewModels;

/// <summary>One entry in the process list with its current connection count.</summary>
public sealed class ProcessItemViewModel
{
    public ProcessItemViewModel(int pid, string name, int connectionCount, DateTime lastActivityUtc)
    {
        Pid = pid;
        Name = name;
        ConnectionCount = connectionCount;
        LastActivityUtc = lastActivityUtc;
    }

    public int Pid { get; }

    public string Name { get; }

    public int ConnectionCount { get; }

    public DateTime LastActivityUtc { get; }

    public string Display => $"{Name} ({Pid}) · {ConnectionCount}";
}
