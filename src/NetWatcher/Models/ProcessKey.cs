namespace NetWatcher.Models;

/// <summary>
/// Identifies a process instance. <see cref="StartTimeUtc"/> guards against PID reuse.
/// </summary>
public sealed record ProcessKey(int Pid, DateTime StartTimeUtc);

/// <summary>Resolved process metadata for a <see cref="ProcessKey"/>.</summary>
public sealed record ProcessInfo(ProcessKey Key, string Name, string? Path);
