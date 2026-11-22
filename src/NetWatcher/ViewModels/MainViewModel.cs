using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using NetWatcher.Logging;
using NetWatcher.Models;
using NetWatcher.Services.Connections;
using NetWatcher.Services.Correlation;
using NetWatcher.Services.Export;
using NetWatcher.Services.ReverseDns;
using NetWatcher.Storage;
using NetWatcher.Utils;

namespace NetWatcher.ViewModels;

/// <summary>
/// UI state: a 1 Hz snapshot of the store with domain bindings, in-place row
/// updates, filters, pause, CSV export, and PTR enrichment.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly NetworkStore _store;
    private readonly DomainCorrelator _correlator;
    private readonly ReverseDnsResolver _ptrResolver;
    private readonly ProcessResolver _resolver;
    private readonly FileLogger _logger;
    private readonly bool _isAdministrator;
    private readonly ConcurrentDictionary<IPAddress, string?> _ptrResults = new();
    private readonly ConcurrentDictionary<IPAddress, byte> _ptrInFlight = new();
    private readonly Dictionary<string, ConnectionRowViewModel> _rowCache = new(StringComparer.Ordinal);
    private readonly List<ConnectionRowViewModel> _allRows = new();

    private IReadOnlyList<ProcessItemViewModel> _processes = Array.Empty<ProcessItemViewModel>();
    private string _filterText = string.Empty;
    private ProcessItemViewModel? _selectedProcess;
    private bool _paused;
    private bool _etwAvailable;
    private int _errorCount;

    public MainViewModel(
        NetworkStore store,
        DomainCorrelator correlator,
        ReverseDnsResolver ptrResolver,
        ProcessResolver resolver,
        FileLogger logger,
        bool etwAvailable = true,
        bool isAdministrator = false)
    {
        _store = store;
        _correlator = correlator;
        _ptrResolver = ptrResolver;
        _resolver = resolver;
        _logger = logger;
        _etwAvailable = etwAvailable;
        _isAdministrator = isAdministrator;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// The visible rows. One collection instance for the window's lifetime, so a
    /// column sort, selection, and scroll position survive refreshes.
    /// </summary>
    public ObservableCollection<ConnectionRowViewModel> Rows { get; } = new();

    public IReadOnlyList<ProcessItemViewModel> Processes => _processes;

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText == value)
            {
                return;
            }

            _filterText = value;
            ApplyFilter();
        }
    }

    public ProcessItemViewModel? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            if (_selectedProcess == value)
            {
                return;
            }

            _selectedProcess = value;
            ApplyFilter();
            OnPropertyChanged(nameof(SelectedProcess));
        }
    }

    public bool Paused => _paused;

    public bool EtwAvailable => _etwAvailable;

    public bool IsAdministrator => _isAdministrator;

    /// <summary>Parsing and polling errors seen this session; shown in the status bar.</summary>
    public int ErrorCount => Volatile.Read(ref _errorCount);

    public string StatusLine =>
        $"Admin: {(_isAdministrator ? "yes" : "no")} | ETW: {(_etwAvailable ? "running" : "unavailable")} | " +
        $"events: {_allRows.Count} | connections: {Rows.Count} | DNS: {_store.GetDnsSnapshot().Count} | " +
        $"errors: {ErrorCount}";

    /// <summary>
    /// Marks ETW as lost (or recovered) so the warning banner and the status bar
    /// stop claiming the session is running. Safe to call from any thread that
    /// reaches it through the UI dispatcher.
    /// </summary>
    public void SetEtwAvailable(bool available)
    {
        if (_etwAvailable == available)
        {
            return;
        }

        _etwAvailable = available;
        OnPropertyChanged(nameof(EtwAvailable));
        OnPropertyChanged(nameof(StatusLine));
    }

    /// <summary>Counts one non-fatal error; thread-safe, reflected on the next refresh.</summary>
    public void CountError() => Interlocked.Increment(ref _errorCount);

    /// <summary>One refresh cycle: snapshot, correlate, PTR-enrich, filter. No-op while paused.</summary>
    public void Tick()
    {
        if (_paused)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        _allRows.Clear();

        foreach (var connection in _store.GetConnectionSnapshot())
        {
            // Inbound connections are collected but hidden in v1 (spec §4).
            if (connection.Inbound)
            {
                continue;
            }

            var key = ConnectionRowViewModel.KeyOf(connection);
            if (!seen.Add(key))
            {
                continue;
            }

            if (!_rowCache.TryGetValue(key, out var row))
            {
                row = new ConnectionRowViewModel(connection, new DomainBinding(null, DomainOrigin.None));
                _rowCache[key] = row;
            }

            var binding = _correlator.Bind(connection, now);

            if (binding.Origin == DomainOrigin.None)
            {
                if (_ptrResults.TryGetValue(connection.RemoteAddress, out var host) &&
                    !string.IsNullOrEmpty(host))
                {
                    binding = new DomainBinding(host, DomainOrigin.Ptr);
                }
                else
                {
                    RequestPtr(connection.RemoteAddress);
                }
            }

            var name = _resolver.GetCached(connection.Process)?.Name ?? $"pid:{connection.Process.Pid}";
            row.Update(connection, binding, name);
            _allRows.Add(row);
        }

        foreach (var stale in _rowCache.Keys.Where(key => !seen.Contains(key)).ToArray())
        {
            _rowCache.Remove(stale);
        }

        RebuildProcesses();
        ApplyFilter();
    }

    public void TogglePause()
    {
        _paused = !_paused;
        OnPropertyChanged(nameof(Paused));
    }

    public void Clear()
    {
        _store.Clear();
        _ptrResults.Clear();
        _rowCache.Clear();
        _allRows.Clear();
        Rows.Clear();
        _processes = Array.Empty<ProcessItemViewModel>();
        _selectedProcess = null;
        OnPropertyChanged(nameof(Processes));
        OnPropertyChanged(nameof(SelectedProcess));
        OnPropertyChanged(nameof(StatusLine));
    }

    /// <summary>Writes the filtered view to <paramref name="path"/>; returns false on I/O failure.</summary>
    public bool ExportCsv(string path)
    {
        try
        {
            File.WriteAllText(path, CsvExporter.ToCsv(Rows));
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"CSV export failed: {ex.Message}");
            return false;
        }
    }

    private void RebuildProcesses()
    {
        var selectedPid = _selectedProcess?.Pid;

        _processes = _allRows
            .GroupBy(row => (row.Pid, row.ProcessName))
            .Select(group => new ProcessItemViewModel(
                group.Key.Pid,
                group.Key.ProcessName,
                group.Count(),
                group.Max(row => row.Record.LastSeenUtc)))
            .OrderByDescending(process => process.ConnectionCount)
            .ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (selectedPid is not null)
        {
            _selectedProcess = _processes.FirstOrDefault(process => process.Pid == selectedPid);
            OnPropertyChanged(nameof(SelectedProcess));
        }

        OnPropertyChanged(nameof(Processes));
    }

    private void ApplyFilter()
    {
        IEnumerable<ConnectionRowViewModel> rows = _allRows;

        if (_selectedProcess is not null)
        {
            rows = rows.Where(row => row.Pid == _selectedProcess.Pid);
        }

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            var text = _filterText.Trim();
            rows = rows.Where(row =>
                row.DomainDisplay.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.Ip.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.ProcessName.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        SyncRows(rows.ToList());
        OnPropertyChanged(nameof(StatusLine));
    }

    /// <summary>
    /// Brings <see cref="Rows"/> in line with <paramref name="visible"/> without
    /// rebuilding it: vanished rows are removed, new rows appended, and the
    /// user's sort and selection left intact.
    /// </summary>
    private void SyncRows(List<ConnectionRowViewModel> visible)
    {
        var desired = new HashSet<ConnectionRowViewModel>(visible);

        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(Rows[i]))
            {
                Rows.RemoveAt(i);
            }
        }

        var present = new HashSet<ConnectionRowViewModel>(Rows);

        foreach (var row in visible)
        {
            if (present.Add(row))
            {
                Rows.Add(row);
            }
        }
    }

    private void RequestPtr(IPAddress address)
    {
        if (!IpHelpers.IsPublic(address) ||
            _ptrResults.ContainsKey(address) ||
            !_ptrInFlight.TryAdd(address, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                _ptrResults[address] = await _ptrResolver.ResolveAsync(address).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Warn($"PTR lookup failed for {address}: {ex.Message}");
            }
            finally
            {
                _ptrInFlight.TryRemove(address, out _);
            }
        });
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
