using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using NetWatcher.Localization;
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
/// updates, filters, pause, exports, and PTR enrichment.
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
    private readonly Dictionary<int, ProcessItemViewModel> _processCache = new();
    private readonly Dictionary<int, ProcessFilterOption> _processOptionCache = new();
    private readonly PauseState _pause;

    private string _filterText = string.Empty;
    private ProcessFilterOption _selectedProcess = ProcessFilterOption.All;
    private bool _uniqueIpsOnly;
    private bool _etwAvailable;
    private int _errorCount;

    public MainViewModel(
        NetworkStore store,
        DomainCorrelator correlator,
        ReverseDnsResolver ptrResolver,
        ProcessResolver resolver,
        FileLogger logger,
        bool etwAvailable = true,
        bool isAdministrator = false,
        PauseState? pause = null)
    {
        _store = store;
        _correlator = correlator;
        _ptrResolver = ptrResolver;
        _resolver = resolver;
        _logger = logger;
        _etwAvailable = etwAvailable;
        _isAdministrator = isAdministrator;
        _pause = pause ?? new PauseState();
        _pause.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Paused));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// The visible rows. One collection instance for the window's lifetime, so a
    /// column sort, selection, and scroll position survive refreshes.
    /// </summary>
    public ObservableCollection<ConnectionRowViewModel> Rows { get; } = new();

    /// <summary>
    /// The process filter options: "All processes" first, then one stable
    /// instance per PID.
    /// </summary>
    public ObservableCollection<ProcessFilterOption> ProcessOptions { get; } =
        new() { ProcessFilterOption.All };

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
            NotifyEmptyState();
        }
    }

    public ProcessFilterOption? SelectedProcess
    {
        get => _selectedProcess;
        set => SelectProcess(value ?? ProcessFilterOption.All);
    }

    /// <summary>
    /// Shows one row per remote address: duplicates from other ports and
    /// processes are collapsed onto the most recently active connection.
    /// </summary>
    public bool UniqueIpsOnly
    {
        get => _uniqueIpsOnly;
        set
        {
            if (_uniqueIpsOnly == value)
            {
                return;
            }

            _uniqueIpsOnly = value;
            ApplyFilter();
            NotifyEmptyState();
        }
    }

    public bool Paused => _pause.IsPaused;

    /// <summary>True when a text filter or a process focus narrows the table.</summary>
    public bool IsViewFiltered =>
        _selectedProcess.Process is not null ||
        _uniqueIpsOnly ||
        !string.IsNullOrWhiteSpace(_filterText);

    public string EmptyTitle => IsViewFiltered ? Strings.NoMatchingConnectionsTitle : Strings.NoConnectionsTitle;

    public string EmptyMessage => IsViewFiltered
        ? Strings.NoMatchingConnectionsMessage
        : Strings.NoConnectionsMessage;

    /// <summary>The row count shown next to the page title.</summary>
    public string VisibleCountText => string.Format(CultureInfo.CurrentCulture, Strings.VisibleCountFormat, Rows.Count);

    public bool EtwAvailable => _etwAvailable;

    public bool IsAdministrator => _isAdministrator;

    /// <summary>Parsing and polling errors seen this session; shown in the status bar.</summary>
    public int ErrorCount => Volatile.Read(ref _errorCount);

    public string StatusLine => string.Format(
        CultureInfo.CurrentCulture,
        Strings.StatusLineFormat,
        _isAdministrator ? Strings.Yes : Strings.No,
        _etwAvailable ? Strings.EtwRunning : Strings.EtwUnavailable,
        _allRows.Count,
        Rows.Count,
        _store.GetDnsSnapshot().Count,
        ErrorCount);

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
        if (_pause.IsPaused)
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

            var info = _resolver.GetCached(connection.Process);
            var name = info?.Name ?? $"pid:{connection.Process.Pid}";
            row.Update(connection, binding, name, info?.Path);
            _allRows.Add(row);
        }

        foreach (var stale in _rowCache.Keys.Where(key => !seen.Contains(key)).ToArray())
        {
            _rowCache.Remove(stale);
        }

        RebuildProcesses();
        ApplyFilter();
    }

    public void TogglePause() => _pause.Toggle();

    public void Clear()
    {
        _store.Clear();
        _ptrResults.Clear();
        _rowCache.Clear();
        _allRows.Clear();
        _processCache.Clear();
        _processOptionCache.Clear();
        Rows.Clear();
        ProcessOptions.Clear();
        ProcessOptions.Add(ProcessFilterOption.All);
        _selectedProcess = ProcessFilterOption.All;
        OnPropertyChanged(nameof(SelectedProcess));
        OnPropertyChanged(nameof(VisibleCountText));
        OnPropertyChanged(nameof(StatusLine));
        NotifyEmptyState();
    }

    /// <summary>Writes the filtered view in <paramref name="format"/>; returns false on I/O failure.</summary>
    public bool Export(string path, ExportFormat format)
    {
        try
        {
            var content = format switch
            {
                ExportFormat.Markdown => MarkdownExporter.ToMarkdown(Rows),
                ExportFormat.Json => JsonExporter.ToJson(Rows),
                _ => CsvExporter.ToCsv(Rows),
            };

            File.WriteAllText(path, content);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"{format} export failed: {ex.Message}");
            return false;
        }
    }

    private void RebuildProcesses()
    {
        var groups = _allRows
            .GroupBy(row => row.Pid)
            .Select(group => new
            {
                Pid = group.Key,
                Name = group.First().ProcessName,
                Path = _resolver.GetCached(group.First().Record.Process)?.Path,
                Count = group.Count(),
                Last = group.Max(row => row.Record.LastSeenUtc),
            })
            .OrderByDescending(process => process.Count)
            .ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var seen = new HashSet<int>(groups.Length);

        foreach (var group in groups)
        {
            seen.Add(group.Pid);

            if (!_processCache.TryGetValue(group.Pid, out var item))
            {
                item = new ProcessItemViewModel(group.Pid, group.Name, group.Path, group.Count, group.Last);
                _processCache[group.Pid] = item;
            }
            else
            {
                item.Update(group.Name, group.Path, group.Count, group.Last);
            }

            if (!_processOptionCache.ContainsKey(group.Pid))
            {
                var option = new ProcessFilterOption(item);
                _processOptionCache[group.Pid] = option;
                ProcessOptions.Add(option);
            }
        }

        foreach (var stalePid in _processCache.Keys.Where(pid => !seen.Contains(pid)).ToArray())
        {
            _processCache.Remove(stalePid);

            if (_processOptionCache.Remove(stalePid, out var staleOption))
            {
                ProcessOptions.Remove(staleOption);
            }
        }

        if (_selectedProcess.Process is { } selected && !seen.Contains(selected.Pid))
        {
            SelectProcess(ProcessFilterOption.All);
        }
    }

    private void SelectProcess(ProcessFilterOption option)
    {
        if (ReferenceEquals(_selectedProcess, option))
        {
            return;
        }

        _selectedProcess = option;
        ApplyFilter();
        OnPropertyChanged(nameof(SelectedProcess));
        NotifyEmptyState();
    }

    private void ApplyFilter()
    {
        IEnumerable<ConnectionRowViewModel> rows = _allRows;

        if (_selectedProcess.Process is { } process)
        {
            rows = rows.Where(row => row.Pid == process.Pid);
        }

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            var text = _filterText.Trim();
            rows = rows.Where(row =>
                row.DomainDisplay.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.Ip.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.ProcessName.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        if (_uniqueIpsOnly)
        {
            rows = rows
                .GroupBy(row => row.Ip, StringComparer.Ordinal)
                .Select(group => group.MaxBy(row => row.Record.LastSeenUtc)!);
        }

        SyncRows(rows.ToList());
        OnPropertyChanged(nameof(VisibleCountText));
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

    private void NotifyEmptyState()
    {
        OnPropertyChanged(nameof(IsViewFiltered));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
