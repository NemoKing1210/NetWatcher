using System.Net;
using NetWatcher.Localization;
using NetWatcher.Models;
using NetWatcher.Services.Connections;
using NetWatcher.Storage;
using NetWatcher.ViewModels;

namespace NetWatcher.Tests;

public sealed class DnsPageViewModelTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ProcessKey Process(int pid) => new(pid, T0.AddDays(-1));

    private static DnsRecord Record(int pid, string name, string address, DateTime at) =>
        new(Process(pid), name, new[] { IPAddress.Parse(address) }, at, DnsStatus.Success);

    private static DnsPageViewModel Create(NetworkStore store, int maxRows = 2000) =>
        new(store, new ProcessResolver(), new PauseState(), maxRows);

    [Fact]
    public void Tick_ShowsNewestFirst()
    {
        var store = new NetworkStore();
        store.AddDns(Record(100, "old.example", "203.0.113.1", T0));
        store.AddDns(Record(100, "new.example", "203.0.113.2", T0.AddMinutes(1)));
        var viewModel = Create(store);

        viewModel.Tick();

        Assert.Equal(new[] { "new.example", "old.example" }, viewModel.Rows.Select(r => r.Domain));
    }

    [Fact]
    public void Tick_CapsRowsAndReportsTotal()
    {
        var store = new NetworkStore();
        store.AddDns(Record(100, "a.example", "203.0.113.1", T0));
        store.AddDns(Record(100, "b.example", "203.0.113.2", T0.AddMinutes(1)));
        store.AddDns(Record(100, "c.example", "203.0.113.3", T0.AddMinutes(2)));
        var viewModel = Create(store, maxRows: 2);

        viewModel.Tick();

        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Equal(3, viewModel.TotalCount);
        Assert.Equal(string.Format(Strings.DnsSummaryFormat, 2, 3), viewModel.Summary);
        Assert.Equal("c.example", viewModel.Rows[0].Domain);
    }

    [Fact]
    public void FilterText_MatchesDomainProcessOrAddress()
    {
        var store = new NetworkStore();
        store.AddDns(Record(100, "example.com", "203.0.113.1", T0));
        store.AddDns(Record(200, "other.net", "198.51.100.7", T0.AddMinutes(1)));
        var viewModel = Create(store);
        viewModel.Tick();

        viewModel.FilterText = "example.com";
        Assert.Single(viewModel.Rows);

        viewModel.FilterText = "198.51.100";
        Assert.Single(viewModel.Rows);

        viewModel.FilterText = "pid:200";
        Assert.Single(viewModel.Rows);

        viewModel.FilterText = string.Empty;
        Assert.Equal(2, viewModel.Rows.Count);
    }

    [Fact]
    public void PausedState_FreezesRows()
    {
        var store = new NetworkStore();
        var pause = new PauseState();
        var viewModel = new DnsPageViewModel(store, new ProcessResolver(), pause);
        viewModel.Tick();

        pause.Toggle();
        store.AddDns(Record(100, "late.example", "203.0.113.9", T0));
        viewModel.Tick();

        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public void Tick_ReusesRowInstancesForUnchangedRecords()
    {
        var store = new NetworkStore();
        store.AddDns(Record(100, "example.com", "203.0.113.1", T0));
        var viewModel = Create(store);
        viewModel.Tick();
        var row = Assert.Single(viewModel.Rows);

        viewModel.Tick();

        Assert.Same(row, Assert.Single(viewModel.Rows));
    }

    [Fact]
    public void Tick_KeepsNewestFirstWhenNewRecordsArriveAndOldOnesSurvive()
    {
        var store = new NetworkStore();
        store.AddDns(Record(100, "old.example", "203.0.113.1", T0));
        var viewModel = Create(store);
        viewModel.Tick();

        store.AddDns(Record(100, "mid.example", "203.0.113.2", T0.AddMinutes(1)));
        store.AddDns(Record(100, "new.example", "203.0.113.3", T0.AddMinutes(2)));
        viewModel.Tick();

        Assert.Equal(
            new[] { "new.example", "mid.example", "old.example" },
            viewModel.Rows.Select(r => r.Domain));
    }

    [Fact]
    public void EmptyState_DistinguishesNoAnswersFromNoMatches()
    {
        var viewModel = Create(new NetworkStore());

        Assert.Equal(Strings.NoDnsTitle, viewModel.EmptyTitle);
        Assert.False(viewModel.IsFiltered);

        viewModel.FilterText = "nothing-matches";

        Assert.Equal(Strings.NoMatchingDnsTitle, viewModel.EmptyTitle);
        Assert.True(viewModel.IsFiltered);
    }

    [Fact]
    public void ClearingTheFilterRestoresNewestFirst()
    {
        var store = new NetworkStore();
        store.AddDns(Record(100, "a.example", "203.0.113.1", T0));
        store.AddDns(Record(100, "b.example", "203.0.113.2", T0.AddMinutes(1)));
        store.AddDns(Record(100, "c.example", "203.0.113.3", T0.AddMinutes(2)));
        var viewModel = Create(store);
        viewModel.Tick();

        viewModel.FilterText = "a.example";
        viewModel.FilterText = string.Empty;

        Assert.Equal(
            new[] { "c.example", "b.example", "a.example" },
            viewModel.Rows.Select(r => r.Domain));
    }

    [Fact]
    public void TogglePause_FlipsTheSharedState()
    {
        var store = new NetworkStore();
        var pause = new PauseState();
        var viewModel = new DnsPageViewModel(store, new ProcessResolver(), pause);

        viewModel.TogglePause();

        Assert.True(pause.IsPaused);
        Assert.True(viewModel.IsPaused);
    }
}
