using System.Net;
using NetWatcher.Logging;
using NetWatcher.Models;
using NetWatcher.Services.Connections;
using NetWatcher.Services.Storage;
using NetWatcher.Storage;

namespace NetWatcher.Tests;

public sealed class AggregatorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Aggregator Create(NetworkStore store, int capacity = 100) =>
        new(
            store,
            new ProcessResolver(),
            new FileLogger(Path.Combine(Path.GetTempPath(), "netwatcher-tests-" + Guid.NewGuid().ToString("N"))),
            queueCapacity: capacity);

    private static ConnectionEvent Event(int localPort, DateTime timestamp) =>
        new(
            new ProcessKey(42, T0.AddDays(-1)),
            Protocol.Tcp,
            IPAddress.Parse("1.2.3.4"),
            443,
            localPort,
            Inbound: false,
            ConnectionSource.Etw,
            timestamp);

    [Fact]
    public void FlushOnce_DrainsQueueIntoStore()
    {
        var store = new NetworkStore();
        var aggregator = Create(store);
        aggregator.EnqueueConnection(Event(50001, T0));
        aggregator.EnqueueConnection(Event(50002, T0));
        aggregator.EnqueueConnection(Event(50003, T0));

        aggregator.FlushOnce();

        Assert.Equal(3, store.GetConnectionSnapshot().Count);
    }

    [Fact]
    public void Dedup_AttemptedAndConnectWithin5s()
    {
        var store = new NetworkStore();
        var aggregator = Create(store);
        aggregator.EnqueueConnection(Event(50001, T0));
        aggregator.EnqueueConnection(Event(50001, T0.AddSeconds(1)));

        aggregator.FlushOnce();

        Assert.Single(store.GetConnectionSnapshot());
    }

    [Fact]
    public void Dedup_KeepsConnectOverAttempted()
    {
        var store = new NetworkStore();
        var aggregator = Create(store);
        aggregator.EnqueueConnection(Event(50001, T0));
        aggregator.EnqueueConnection(Event(50001, T0.AddSeconds(1)));

        aggregator.FlushOnce();

        var row = Assert.Single(store.GetConnectionSnapshot());
        Assert.Equal(T0.AddSeconds(1), row.LastSeenUtc);
    }

    [Fact]
    public void BoundedQueue_DropsOldestWithoutBlocking()
    {
        var store = new NetworkStore();
        var aggregator = Create(store, capacity: 2);
        aggregator.EnqueueConnection(Event(50001, T0));
        aggregator.EnqueueConnection(Event(50002, T0));
        aggregator.EnqueueConnection(Event(50003, T0));

        aggregator.FlushOnce();

        var rows = store.GetConnectionSnapshot();
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, row => row.LocalPort == 50001);
    }
}
