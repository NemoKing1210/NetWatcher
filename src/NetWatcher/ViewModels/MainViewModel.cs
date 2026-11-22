using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Net;
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
/// UI state: a 1 Hz snapshot of the store with domain bindings, diff-free row
/// rebuilding, filters, pause, CSV export, and PTR enrichment.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly NetworkStore _store;
    private readonly DomainCorrelator _correlator;
    private readonly ReverseDnsResolver _ptrResolver;
    private readonly ProcessResolver _resolver;
    private readonly FileLogger _logger;
    private readonly bool _etwAvailable;
    private readonly bool _isAdministrator;
    private readonly ConcurrentDictionary<IPAddress, string?> _ptrResults = new();
    private readonly ConcurrentDictionary<IPAddress, byte> _ptrInFlight = new();

    private List<ConnectionRowViewModel> _allRows = new();
    private IReadOnlyList<ConnectionRowViewModel> _rows = Array.Empty<ConnectionRowViewModel>();
    private IReadOnlyList<ProcessItemViewModel> _processes = Array.Empty<ProcessItemViewModel>();
    private string _filterText = string.Empty;
    private ProcessItemViewModel? _selectedProcess;
    private bool _paused;

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

    public IReadOnlyList<ConnectionRowViewModel> Rows => _rows;

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

    public string StatusLine =>
        $"Admin: {(_isAdministrator ? "yes" : "no")} | ETW: {(_etwAvailable ? "running" : "unavailable")} | " +
        $"events: {_allRows.Count} | connections: {_rows.Count} | DNS: {_store.GetDnsSnapshot().Count} | errors: 0";

    /// <summary>One refresh cycle: snapshot, correlate, PTR-enrich, filter. No-op while paused.</summary>
    public void Tick()
    {
        if (_paused)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var rows = new List<ConnectionRowViewModel>();

        foreach (var connection in _store.GetConnectionSnapshot())
        {
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
            rows.Add(new ConnectionRowViewModel(connection, binding, name));
        }

        _allRows = rows;
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
        _allRows = new List<ConnectionRowViewModel>();
        _processes = Array.Empty<ProcessItemViewModel>();
        _rows = Array.Empty<ConnectionRowViewModel>();
        OnPropertyChanged(nameof(Processes));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(StatusLine));
    }

    /// <summary>Writes the filtered view to <paramref name="path"/>; returns false on I/O failure.</summary>
    public bool ExportCsv(string path)
    {
        try
        {
            File.WriteAllText(path, CsvExporter.ToCsv(_rows));
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

        _rows = rows.ToArray();
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(StatusLine));
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
