using System.IO;

namespace NetWatcher.Logging;

/// <summary>
/// Minimal thread-safe file logger. Writes to
/// <c>&lt;baseDirectory ?? %LOCALAPPDATA%\NetWatcher\logs&gt;\netwatcher-yyyyMMdd.log</c>.
/// </summary>
public sealed class FileLogger
{
    private readonly string _directory;
    private readonly object _sync = new();
    private bool _directoryReady;

    public FileLogger(string? baseDirectory = null)
    {
        _directory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetWatcher",
            "logs");
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var now = DateTime.UtcNow;
        var line = $"{now:yyyy-MM-ddTHH:mm:ss.fffZ} [{level}] {message}{Environment.NewLine}";
        var file = Path.Combine(_directory, $"netwatcher-{now:yyyyMMdd}.log");

        try
        {
            lock (_sync)
            {
                if (!_directoryReady)
                {
                    Directory.CreateDirectory(_directory);
                    _directoryReady = true;
                }

                File.AppendAllText(file, line);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never break a collector or an ETW callback.
            _directoryReady = false;
        }
    }
}
