using System.Net;
using System.Net.NetworkInformation;
using NetWatcher.Localization;
using NetWatcher.Logging;
using NetWatcher.Models;
using NetWatcher.Services.Connections;
using NetWatcher.Services.Correlation;
using NetWatcher.Services.Export;
using NetWatcher.Services.ReverseDns;
using NetWatcher.Storage;
using NetWatcher.Utils;
using NetWatcher.ViewModels;

namespace NetWatcher.Tests;

public sealed class MainViewModelTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ProcessKey Process(int pid) => new(pid, T0.AddDays(-1));

    private static IEnumerable<ProcessFilterOption> LiveProcesses(MainViewModel viewModel) =>
        viewModel.ProcessOptions.Where(option => !option.IsAll);

    private static ConnectionRecord Connection(int pid, string ip, DateTime seen) =>
        new(
            Process(pid),
            Protocol.Tcp,
            IPAddress.Parse(ip),
            443,
            50000,
            TcpState.Established,
            ConnectionSource.Etw,
            seen,
            seen,
            Inbound: false);

    private static DnsRecord Dns(int pid, string name, string ip, DateTime timestamp) =>
        new(Process(pid), name, new[] { IPAddress.Parse(ip) }, timestamp, DnsStatus.Success);

    private static MainViewModel Create(
        NetworkStore store,
        bool etwAvailable = true,
        bool isAdministrator = true,
        PauseState? pause = null) =>
        new(
            store,
            new DomainCorrelator(store),
            new ReverseDnsResolver((_, _) => Task.FromResult<string?>(null), TimeProvider.System),
            new ProcessResolver(),
            new FileLogger(Path.Combine(Path.GetTempPath(), "netwatcher-tests-" + Guid.NewGuid().ToString("N"))),
            etwAvailable,
            isAdministrator,
            pause);

    [Fact]
    public void PauseState_ToggleRaisesPropertyChanged()
    {
        var state = new PauseState();
        var changed = new List<string?>();
        state.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        state.Toggle();

        Assert.True(state.IsPaused);
        Assert.Contains(nameof(PauseState.IsPaused), changed);
    }

    [Fact]
    public void Tick_IsFrozenBySharedPauseState()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        var pause = new PauseState();
        var viewModel = Create(store, pause: pause);
        viewModel.Tick();

        pause.Toggle();
        store.UpsertConnection(Connection(101, "203.0.113.6", T0));
        viewModel.Tick();
        Assert.Single(viewModel.Rows);

        pause.Toggle();
        viewModel.Tick();
        Assert.Equal(2, viewModel.Rows.Count);
    }

    [Fact]
    public void Tick_ReusesProcessItemInstancesAndUpdatesCounts()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        var viewModel = Create(store);
        viewModel.Tick();
        var item = Assert.Single(LiveProcesses(viewModel));

        store.UpsertConnection(Connection(100, "203.0.113.6", T0));
        viewModel.Tick();

        Assert.Same(item, Assert.Single(LiveProcesses(viewModel)));
        Assert.Equal(2, item.Process!.ConnectionCount);
        Assert.Contains("2", item.Display);
    }

    [Fact]
    public void Tick_PopulatesRowsFromStoreSnapshot()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        store.AddDns(Dns(100, "example.com", "203.0.113.5", T0));
        var viewModel = Create(store);

        viewModel.Tick();

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("example.com", row.DomainDisplay);
    }

    [Fact]
    public void PausedFreezesRowsWhileStoreGrows()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        var viewModel = Create(store);
        viewModel.Tick();
        viewModel.TogglePause();

        store.UpsertConnection(Connection(101, "203.0.113.6", T0));
        viewModel.Tick();

        Assert.Single(viewModel.Rows);

        viewModel.TogglePause();
        viewModel.Tick();

        Assert.Equal(2, viewModel.Rows.Count);
    }

    [Fact]
    public void Tick_ReusesRowInstancesAcrossRefreshes()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        var viewModel = Create(store);
        viewModel.Tick();

        var rows = viewModel.Rows;
        var row = Assert.Single(viewModel.Rows);

        store.UpsertConnection(Connection(100, "203.0.113.5", T0.AddSeconds(5)));
        viewModel.Tick();

        Assert.Same(rows, viewModel.Rows);
        Assert.Same(row, Assert.Single(viewModel.Rows));
        Assert.Equal(T0.AddSeconds(5), row.Record.LastSeenUtc);
    }

    [Fact]
    public void Tick_NotifiesWhenRowContentChanges()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        var viewModel = Create(store);
        viewModel.Tick();

        var row = Assert.Single(viewModel.Rows);
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        store.UpsertConnection(Connection(100, "203.0.113.5", T0.AddMinutes(5)));
        viewModel.Tick();

        Assert.Contains(nameof(ConnectionRowViewModel.LastActivity), changed);
    }

    [Fact]
    public void Tick_HidesInboundConnections()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        store.UpsertConnection(Connection(200, "203.0.113.6", T0) with { Inbound = true });
        var viewModel = Create(store);

        viewModel.Tick();

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal(100, row.Pid);
        Assert.Equal(100, Assert.Single(LiveProcesses(viewModel)).Process!.Pid);
    }

    [Fact]
    public void Clear_EmptiesRows()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        var viewModel = Create(store);
        viewModel.Tick();

        viewModel.Clear();

        Assert.Empty(viewModel.Rows);
        Assert.True(Assert.Single(viewModel.ProcessOptions).IsAll);
    }

    [Fact]
    public void SetEtwAvailable_UpdatesBannerFlagAndStatusLine()
    {
        var viewModel = Create(new NetworkStore(), etwAvailable: true);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.SetEtwAvailable(false);

        Assert.False(viewModel.EtwAvailable);
        Assert.Contains(Strings.EtwUnavailable, viewModel.StatusLine);
        Assert.Contains(nameof(MainViewModel.EtwAvailable), changed);
        Assert.Contains(nameof(MainViewModel.StatusLine), changed);
    }

    [Fact]
    public void CountError_ShowsInStatusLine()
    {
        var viewModel = Create(new NetworkStore());

        viewModel.CountError();
        viewModel.CountError();

        var expected = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            Strings.StatusLineFormat,
            Strings.Yes,
            Strings.EtwRunning,
            0,
            0,
            0,
            2);

        Assert.Equal(expected, viewModel.StatusLine);
    }

    [Fact]
    public void FilterText_MatchesDomainOrIp()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        store.UpsertConnection(Connection(100, "203.0.113.6", T0));
        store.AddDns(Dns(100, "example.com", "203.0.113.5", T0));
        var viewModel = Create(store);
        viewModel.Tick();

        viewModel.FilterText = "203.0.113.5";
        Assert.Single(viewModel.Rows);

        viewModel.FilterText = "example.com";
        Assert.Single(viewModel.Rows);

        viewModel.FilterText = string.Empty;
        Assert.Equal(2, viewModel.Rows.Count);
    }

    [Fact]
    public void UniqueIpsOnly_CollapsesDuplicatesAndRestoresOnToggleOff()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        store.UpsertConnection(Connection(101, "203.0.113.5", T0.AddMinutes(1)));
        store.UpsertConnection(Connection(100, "203.0.113.6", T0));
        var viewModel = Create(store);
        viewModel.Tick();

        viewModel.UniqueIpsOnly = true;

        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Equal(101, viewModel.Rows.Single(row => row.Ip == "203.0.113.5").Pid);
        Assert.True(viewModel.IsViewFiltered);
        Assert.Equal(string.Format(Strings.VisibleCountFormat, 2), viewModel.VisibleCountText);

        viewModel.UniqueIpsOnly = false;

        Assert.Equal(3, viewModel.Rows.Count);
        Assert.False(viewModel.IsViewFiltered);
    }

    [Fact]
    public void SelectedProcess_FiltersRowsByPid()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        store.UpsertConnection(Connection(101, "203.0.113.6", T0));
        var viewModel = Create(store);
        viewModel.Tick();

        viewModel.SelectedProcess = viewModel.ProcessOptions.Single(o => o.Process?.Pid == 100);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal(100, row.Pid);
    }

    [Fact]
    public void Tick_ResetsSelectionToAllWhenProcessDisappears()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        var viewModel = Create(store);
        viewModel.Tick();
        viewModel.SelectedProcess = viewModel.ProcessOptions.Single(o => o.Process?.Pid == 100);

        store.Clear();
        store.UpsertConnection(Connection(101, "203.0.113.6", T0));
        viewModel.Tick();

        Assert.True(viewModel.SelectedProcess!.IsAll);
        Assert.False(viewModel.IsViewFiltered);
        Assert.Equal(101, Assert.Single(viewModel.Rows).Pid);
    }

    [Fact]
    public void DomainDisplay_SharedPrefixAndPtrAndNone()
    {
        var record = Connection(100, "203.0.113.5", T0);

        Assert.Equal("~shared.example", new ConnectionRowViewModel(record, new DomainBinding("shared.example", DomainOrigin.SharedCdn)).DomainDisplay);
        Assert.Equal("ptr.example", new ConnectionRowViewModel(record, new DomainBinding("ptr.example", DomainOrigin.Ptr)).DomainDisplay);
        Assert.Equal("—", new ConnectionRowViewModel(record, new DomainBinding(null, DomainOrigin.None)).DomainDisplay);
    }

    [Fact]
    public void StatusLine_ShowsAdminAndEtwFlags()
    {
        var store = new NetworkStore();

        var running = Create(store, etwAvailable: true, isAdministrator: true);
        Assert.Contains(Strings.Yes, running.StatusLine);
        Assert.Contains(Strings.EtwRunning, running.StatusLine);

        var limited = Create(store, etwAvailable: false, isAdministrator: false);
        Assert.Contains(Strings.No, limited.StatusLine);
        Assert.Contains(Strings.EtwUnavailable, limited.StatusLine);
    }

    [Fact]
    public void EmptyState_DistinguishesNoTrafficFromNoMatches()
    {
        var store = new NetworkStore();
        var viewModel = Create(store);

        Assert.Equal(Strings.NoConnectionsTitle, viewModel.EmptyTitle);
        Assert.False(viewModel.IsViewFiltered);

        viewModel.FilterText = "nothing-matches";

        Assert.Equal(Strings.NoMatchingConnectionsTitle, viewModel.EmptyTitle);
        Assert.True(viewModel.IsViewFiltered);

        viewModel.FilterText = string.Empty;

        Assert.Equal(Strings.NoConnectionsTitle, viewModel.EmptyTitle);
        Assert.False(viewModel.IsViewFiltered);
    }

    [Theory]
    [InlineData(ExportFormat.Csv, ".csv")]
    [InlineData(ExportFormat.Markdown, ".md")]
    [InlineData(ExportFormat.Json, ".json")]
    public void Export_WritesFilteredView(ExportFormat format, string extension)
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        store.UpsertConnection(Connection(100, "203.0.113.6", T0));
        var viewModel = Create(store);
        viewModel.Tick();
        viewModel.FilterText = "203.0.113.5";

        var path = Path.Combine(
            Path.GetTempPath(),
            "netwatcher-export-" + Guid.NewGuid().ToString("N") + extension);
        try
        {
            Assert.True(viewModel.Export(path, format));

            var exported = File.ReadAllText(path);
            Assert.Contains("203.0.113.5", exported);
            Assert.DoesNotContain("203.0.113.6", exported);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
