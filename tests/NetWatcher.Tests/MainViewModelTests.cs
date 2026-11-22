using System.Net;
using System.Net.NetworkInformation;
using NetWatcher.Logging;
using NetWatcher.Models;
using NetWatcher.Services.Connections;
using NetWatcher.Services.Correlation;
using NetWatcher.Services.ReverseDns;
using NetWatcher.Storage;
using NetWatcher.Utils;
using NetWatcher.ViewModels;

namespace NetWatcher.Tests;

public sealed class MainViewModelTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ProcessKey Process(int pid) => new(pid, T0.AddDays(-1));

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
        bool isAdministrator = true) =>
        new(
            store,
            new DomainCorrelator(store),
            new ReverseDnsResolver((_, _) => Task.FromResult<string?>(null), TimeProvider.System),
            new ProcessResolver(),
            new FileLogger(Path.Combine(Path.GetTempPath(), "netwatcher-tests-" + Guid.NewGuid().ToString("N"))),
            etwAvailable,
            isAdministrator);

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
    public void SelectedProcess_FiltersRowsByPid()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        store.UpsertConnection(Connection(101, "203.0.113.6", T0));
        var viewModel = Create(store);
        viewModel.Tick();

        viewModel.SelectedProcess = viewModel.Processes.Single(p => p.Pid == 100);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal(100, row.Pid);
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
        Assert.Contains("Admin: yes", running.StatusLine);
        Assert.Contains("ETW: running", running.StatusLine);

        var limited = Create(store, etwAvailable: false, isAdministrator: false);
        Assert.Contains("Admin: no", limited.StatusLine);
        Assert.Contains("ETW: unavailable", limited.StatusLine);
    }

    [Fact]
    public void ExportCsv_WritesFilteredView()
    {
        var store = new NetworkStore();
        store.UpsertConnection(Connection(100, "203.0.113.5", T0));
        store.UpsertConnection(Connection(100, "203.0.113.6", T0));
        var viewModel = Create(store);
        viewModel.Tick();
        viewModel.FilterText = "203.0.113.5";

        var path = Path.Combine(Path.GetTempPath(), "netwatcher-export-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            Assert.True(viewModel.ExportCsv(path));

            var csv = File.ReadAllText(path);
            Assert.Contains("203.0.113.5", csv);
            Assert.DoesNotContain("203.0.113.6", csv);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
