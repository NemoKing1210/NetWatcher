using System.Threading.Channels;
using NetWatcher.Logging;
using NetWatcher.Models;
using NetWatcher.Services.Connections;
using NetWatcher.Storage;

namespace NetWatcher.Services.Storage;

/// <summary>
/// Drains ETW events from bounded queues every 250 ms into the store, and
/// evicts beyond the spec's caps. Enqueue is non-blocking; a full queue drops
/// the oldest event rather than stalling the ETW thread.
/// </summary>
public sealed class Aggregator : IDisposable
{
    private const int DefaultQueueCapacity = 50_000;
    private const int MaxConnections = 100_000;
    private const int MaxDns = 100_000;
    private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromMilliseconds(250);

    private readonly NetworkStore _store;
    private readonly ProcessResolver _resolver;
    private readonly FileLogger _logger;
    private readonly TimeSpan _flushInterval;
    private readonly Channel<ConnectionEvent> _connections;
    private readonly Channel<DnsRecord> _dns;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public Aggregator(
        NetworkStore store,
        ProcessResolver resolver,
        FileLogger logger,
        TimeSpan? flushInterval = null,
        int? queueCapacity = null)
    {
        _store = store;
        _resolver = resolver;
        _logger = logger;
        _flushInterval = flushInterval ?? DefaultFlushInterval;

        var capacity = queueCapacity ?? DefaultQueueCapacity;
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        };

        _connections = Channel.CreateBounded<ConnectionEvent>(options);
        _dns = Channel.CreateBounded<DnsRecord>(options);
    }

    /// <summary>Queues a connection event. Never blocks.</summary>
    public void EnqueueConnection(ConnectionEvent connection) =>
        _connections.Writer.TryWrite(connection);

    /// <summary>Queues a DNS record. Never blocks.</summary>
    public void EnqueueDns(DnsRecord record) =>
        _dns.Writer.TryWrite(record);

    /// <summary>Starts the background flush loop.</summary>
    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    /// <summary>Stops the flush loop after the current pass.</summary>
    public void Stop()
    {
        var cts = _cts;
        var loop = _loop;

        cts?.Cancel();

        try
        {
            loop?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Cancellation surfaces here; the loop is already stopping.
        }

        if (ReferenceEquals(_loop, loop))
        {
            _loop = null;
            _cts = null;
            cts?.Dispose();
        }
    }

    public void Dispose()
    {
        Stop();
    }

    /// <summary>One drain-and-store pass. Test hook; also used by the loop.</summary>
    internal void FlushOnce()
    {
        while (_connections.Reader.TryRead(out var connection))
        {
            var processKey = connection.Process;
            if (processKey.StartTimeUtc == DateTime.MinValue)
            {
                processKey = _resolver.GetOrAdd(processKey.Pid).Key;
            }

            _store.UpsertConnection(new ConnectionRecord(
                processKey,
                connection.Protocol,
                connection.RemoteAddress,
                connection.RemotePort,
                connection.LocalPort,
                State: null,
                connection.Source,
                connection.TimestampUtc,
                connection.TimestampUtc,
                connection.Inbound));
        }

        while (_dns.Reader.TryRead(out var record))
        {
            if (record.Process.StartTimeUtc == DateTime.MinValue)
            {
                record = record with { Process = _resolver.GetOrAdd(record.Process.Pid).Key };
            }

            _store.AddDns(record);
        }

        _store.EvictByLastSeen(MaxConnections, MaxDns);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    FlushOnce();
                }
                catch (Exception ex)
                {
                    _logger.Error($"Aggregator flush failed: {ex.Message}");
                }

                await Task.Delay(_flushInterval, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }
}
