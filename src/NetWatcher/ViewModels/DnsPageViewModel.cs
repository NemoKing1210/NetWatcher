using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using NetWatcher.Localization;
using NetWatcher.Models;
using NetWatcher.Services.Connections;
using NetWatcher.Storage;

namespace NetWatcher.ViewModels;

/// <summary>
/// The DNS events page: the newest answers first, capped at
/// <c>maxRows</c>, filtered by a text box, and frozen while the session is
/// paused. Row objects are reused so the list does not rebuild itself every
/// second.
/// </summary>
public sealed class DnsPageViewModel : INotifyPropertyChanged
{
    private readonly NetworkStore _store;
    private readonly ProcessResolver _resolver;
    private readonly PauseState _pause;
    private readonly int _maxRows;
    private readonly List<DnsRowViewModel> _desired = new();

    private Dictionary<string, DnsRowViewModel> _cache = new();
    private string _filterText = string.Empty;
    private int _totalCount;

    public DnsPageViewModel(
        NetworkStore store,
        ProcessResolver resolver,
        PauseState pause,
        int maxRows = 2000)
    {
        _store = store;
        _resolver = resolver;
        _pause = pause;
        _maxRows = maxRows;
        _pause.PropertyChanged += OnPauseChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DnsRowViewModel> Rows { get; } = new();

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText == value)
            {
                return;
            }

            _filterText = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFiltered)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmptyTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmptyMessage)));
            ApplyFilter();
        }
    }

    public bool IsPaused => _pause.IsPaused;

    public bool IsFiltered => !string.IsNullOrWhiteSpace(_filterText);

    public string EmptyTitle => IsFiltered ? Strings.NoMatchingDnsTitle : Strings.NoDnsTitle;

    public string EmptyMessage => IsFiltered
        ? Strings.NoMatchingDnsMessage
        : Strings.NoDnsMessage;

    public int TotalCount => _totalCount;

    public string Summary =>
        _totalCount > Rows.Count
            ? string.Format(CultureInfo.CurrentCulture, Strings.DnsSummaryFormat, Rows.Count, _totalCount)
            : string.Empty;

    /// <summary>The cap note under the table.</summary>
    public string FooterNote =>
        string.Format(CultureInfo.CurrentCulture, Strings.DnsFooterFormat, _maxRows);

    public void TogglePause() => _pause.Toggle();

    /// <summary>Refreshes the list; a no-op while the session is paused.</summary>
    public void Tick()
    {
        if (_pause.IsPaused)
        {
            return;
        }

        var snapshot = _store.GetDnsSnapshot();
        _totalCount = snapshot.Count;

        var desired = new List<DnsRowViewModel>(Math.Min(_maxRows, snapshot.Count));
        var cache = new Dictionary<string, DnsRowViewModel>(desired.Capacity);

        foreach (var record in snapshot.OrderByDescending(r => r.TimestampUtc).Take(_maxRows))
        {
            var key = DnsRowViewModel.KeyOf(record);

            if (!_cache.TryGetValue(key, out var row))
            {
                row = new DnsRowViewModel(record, ProcessNameFor(record));
            }

            cache[key] = row;
            desired.Add(row);
        }

        _cache = cache;
        _desired.Clear();
        _desired.AddRange(desired);

        ApplyFilter();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalCount)));
    }

    private string ProcessNameFor(DnsRecord record) =>
        _resolver.GetCached(record.Process)?.Name ??
        string.Create(CultureInfo.InvariantCulture, $"pid:{record.Process.Pid}");

    private void ApplyFilter()
    {
        IEnumerable<DnsRowViewModel> filtered = _desired;

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            filtered = _desired.Where(Matches);
        }

        SyncRows(filtered.ToList());
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
    }

    private bool Matches(DnsRowViewModel row) =>
        row.Domain.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
        row.Addresses.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
        row.ProcessName.Contains(_filterText, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Brings <see cref="Rows"/> in line with <paramref name="desired"/>: vanished
    /// rows are removed, then missing rows are inserted at their desired position,
    /// walking forwards so the newest rows land above the survivors. Rows that
    /// stay are never moved.
    /// </summary>
    private void SyncRows(IReadOnlyList<DnsRowViewModel> desired)
    {
        var wanted = new HashSet<DnsRowViewModel>(desired);

        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(Rows[i]))
            {
                Rows.RemoveAt(i);
            }
        }

        var present = new HashSet<DnsRowViewModel>(Rows);
        var insertAt = 0;

        foreach (var row in desired)
        {
            if (insertAt < Rows.Count && ReferenceEquals(Rows[insertAt], row))
            {
                insertAt++;
                continue;
            }

            if (present.Add(row))
            {
                Rows.Insert(insertAt, row);
            }

            insertAt++;
        }
    }

    private void OnPauseChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PauseState.IsPaused))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPaused)));
        }
    }
}
