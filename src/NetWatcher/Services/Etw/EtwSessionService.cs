using System.Net;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using NetWatcher.Logging;
using NetWatcher.Models;
using NetWatcher.Utils;

namespace NetWatcher.Services.Etw;

/// <summary>
/// One real-time ETW session for the Kernel-Network and DNS-Client providers.
/// Events are raised on the ETW thread; consumers must enqueue, not block.
/// </summary>
public sealed class EtwSessionService : IDisposable
{
    private static readonly Guid KernelNetworkGuid = new("7dd42a49-5329-4832-8dfd-43d979153a88");
    private static readonly Guid DnsClientGuid = new("1c95126e-7eea-49a9-a3fe-a378b03ddb4d");

    private static readonly string[] TcpTransferEvents =
    {
        "TcpIpSend", "TcpIpReceive", "TcpIpCopy", "TcpIpData", "TcpIpRetransmit", "TcpIpReconnect",
    };

    private readonly FileLogger _logger;
    private readonly object _sync = new();
    private readonly SeenEventIds _unknownDnsIds = new();

    private TraceEventSession? _session;
    private Thread? _thread;
    private int _restarted;
    private volatile bool _stopping;

    public EtwSessionService(FileLogger logger)
    {
        _logger = logger;
    }

    /// <summary>Raised for every normalized TCP/UDP connection event.</summary>
    public event Action<ConnectionEvent>? ConnectionReceived;

    /// <summary>Raised for every parsed DNS answer.</summary>
    public event Action<DnsRecord>? DnsReceived;

    /// <summary>Raised for provider events the parser did not recognize (logged, never thrown).</summary>
    public event Action<string>? UnknownEvent;

    /// <summary>Raised when an event could not be parsed (the UI counts these as errors).</summary>
    public event Action<string>? ParseFailed;

    /// <summary>Raised when the session cannot start or dies twice (the UI shows a banner).</summary>
    public event Action<string>? Failed;

    /// <summary>
    /// Starts the session. Returns false when ETW is unavailable (no admin
    /// rights or provider missing); the caller falls back to table polling.
    /// </summary>
    public bool Start()
    {
        try
        {
            lock (_sync)
            {
                CreateSession();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"ETW start failed: {ex.GetType().Name}: {ex.Message}");
            Failed?.Invoke(ex.Message);
            return false;
        }
    }

    /// <summary>Stops the session and waits briefly for the ETW thread.</summary>
    public void Stop()
    {
        TraceEventSession? session;
        Thread? thread;

        lock (_sync)
        {
            _stopping = true;
            session = _session;
            thread = _thread;
        }

        try
        {
            session?.Stop();
        }
        catch (Exception ex)
        {
            _logger.Warn($"ETW stop failed: {ex.Message}");
        }

        thread?.Join(TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        Stop();

        lock (_sync)
        {
            if (_session is { } session)
            {
                session.Source.AllEvents -= OnEvent;
                session.Dispose();
                _session = null;
            }
        }
    }

    private void CreateSession()
    {
        _session = new TraceEventSession("NetWatcher-" + Guid.NewGuid().ToString("N"));
        _session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
        _session.EnableProvider(DnsClientGuid, TraceEventLevel.Informational);
        _session.Source.AllEvents += OnEvent;

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "NetWatcher.ETW",
        };
        _thread.Start();
    }

    private void Run()
    {
        try
        {
            _session?.Source.Process();
        }
        catch (Exception ex)
        {
            if (_stopping)
            {
                return;
            }

            _logger.Error($"ETW thread failed: {ex.GetType().Name}: {ex.Message}");

            if (Interlocked.Exchange(ref _restarted, 1) == 0)
            {
                try
                {
                    lock (_sync)
                    {
                        if (_stopping)
                        {
                            return;
                        }

                        if (_session is { } session)
                        {
                            session.Source.AllEvents -= OnEvent;
                            session.Dispose();
                            _session = null;
                        }

                        CreateSession();
                    }

                    return;
                }
                catch (Exception retry)
                {
                    _logger.Error($"ETW restart failed: {retry.Message}");
                }
            }

            Failed?.Invoke("ETW session stopped");
        }
    }

    private void OnEvent(TraceEvent data)
    {
        try
        {
            var provider = data.ProviderGuid;
            var providerName = data.ProviderName ?? string.Empty;

            if (provider == KernelNetworkGuid || providerName == "Microsoft-Windows-Kernel-Network")
            {
                HandleKernelEvent(data);
                return;
            }

            if (provider == DnsClientGuid || providerName == "Microsoft-Windows-DNS-Client")
            {
                HandleDnsEvent(data);
            }
        }
        catch (Exception ex)
        {
            ParseFailed?.Invoke($"{data.ProviderName} {data.ID} failed to parse: {ex.Message}");
        }
    }

    private void HandleKernelEvent(TraceEvent data)
    {
        var name = data.EventName ?? string.Empty;

        if (name.Contains("ConnectionAttempted", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Connect", StringComparison.OrdinalIgnoreCase) &&
             !name.Contains("Disconnect", StringComparison.OrdinalIgnoreCase) &&
             !name.Contains("Reconnect", StringComparison.OrdinalIgnoreCase)))
        {
            EmitConnection(data, Protocol.Tcp, inbound: false);
            return;
        }

        if (name.Contains("Accept", StringComparison.OrdinalIgnoreCase))
        {
            EmitConnection(data, Protocol.Tcp, inbound: true);
            return;
        }

        if (name.Contains("UdpIpSend", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("UdpIp", StringComparison.OrdinalIgnoreCase) &&
             name.Contains("Send", StringComparison.OrdinalIgnoreCase)))
        {
            EmitConnection(data, Protocol.Udp, inbound: false);
            return;
        }

        if (name.Contains("UdpIpRecv", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("UdpIp", StringComparison.OrdinalIgnoreCase) &&
             name.Contains("Recv", StringComparison.OrdinalIgnoreCase)))
        {
            EmitConnection(data, Protocol.Udp, inbound: true);
            return;
        }

        if (name.Contains("Disconnect", StringComparison.OrdinalIgnoreCase) ||
            TcpTransferEvents.Any(known => name.Contains(known, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        UnknownEvent?.Invoke($"kernel {data.ID} {name} ({ProviderName(data)})");
    }

    private void EmitConnection(TraceEvent data, Protocol protocol, bool inbound)
    {
        var pid = data.ProcessID > 0 ? data.ProcessID : GetInt(data, "PID");
        if (pid <= 0)
        {
            return;
        }

        var remote = GetAddress(data, "daddr");
        var remotePort = KernelPortToHost(GetInt(data, "dport"));
        var localPort = KernelPortToHost(GetInt(data, "sport"));

        if (remote is null || remotePort is < 0 or > 65535 || localPort is < 0 or > 65535)
        {
            ParseFailed?.Invoke($"{ProviderName(data)} {data.EventName}: missing address or port");
            return;
        }

        var connection = TcpUdpEventParser.Normalize(
            new ProcessKey(pid, DateTime.MinValue),
            NormalizeTimestamp(data.TimeStamp),
            protocol,
            inbound,
            remote.GetAddressBytes(),
            remotePort,
            localPort,
            ConnectionSource.Etw);

        if (connection is null)
        {
            ParseFailed?.Invoke($"{ProviderName(data)} {data.EventName}: invalid address");
            return;
        }

        ConnectionReceived?.Invoke(connection);
    }

    private void HandleDnsEvent(TraceEvent data)
    {
        var queryName = TryGetPayload(data, "QueryName") as string;
        var queryResults = TryGetPayload(data, "QueryResults") as string;

        switch (DnsEventDispatch.Classify(queryName, queryResults))
        {
            case DnsEventKind.Recognized:
                return;

            case DnsEventKind.Unknown:
                if (_unknownDnsIds.TryAdd((int)data.ID))
                {
                    UnknownEvent?.Invoke($"dns {data.ID} {data.EventName}");
                }

                return;
        }

        var pid = data.ProcessID > 0 ? data.ProcessID : GetInt(data, "PID");
        if (pid <= 0)
        {
            return;
        }

        var rawStatus = GetInt(data, "QueryStatus");
        var status = rawStatus == 0 ? DnsStatus.Success : DnsStatus.Error;

        var record = DnsEventParser.Parse(
            new ProcessKey(pid, DateTime.MinValue),
            NormalizeTimestamp(data.TimeStamp),
            queryName!,
            status,
            queryResults!);

        if (record is not null)
        {
            DnsReceived?.Invoke(record);
        }
    }

    /// <summary>
    /// Kernel-Network reports <c>dport</c>/<c>sport</c> in network byte order;
    /// converts a raw payload value to host order and keeps missing or
    /// out-of-range values invalid so the caller can skip the event.
    /// </summary>
    internal static int KernelPortToHost(int rawPort) =>
        rawPort is < 0 or > 65535 ? -1 : IpHelpers.NetworkPortToHost((ushort)rawPort);

    /// <summary>
    /// TraceEvent reports <c>TimeStamp</c> in local time; the store and every
    /// correlation window are UTC, so normalize once at ingress.
    /// </summary>
    internal static DateTime NormalizeTimestamp(DateTime timestamp) =>
        timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();

    private static string ProviderName(TraceEvent data) => data.ProviderName ?? "unknown-provider";

    private static IPAddress? GetAddress(TraceEvent data, string name)
    {
        var value = TryGetPayload(data, name);

        return value switch
        {
            IPAddress address => address,
            byte[] bytes when bytes.Length is 4 or 16 => new IPAddress(bytes),
            string text when IPAddress.TryParse(text, out var parsed) => parsed,
            _ => null,
        };
    }

    private static int GetInt(TraceEvent data, string name)
    {
        var value = TryGetPayload(data, name);

        return value switch
        {
            byte b => b,
            short s => s,
            ushort us => us,
            int i => i,
            uint ui => unchecked((int)ui),
            long l => unchecked((int)l),
            _ => -1,
        };
    }

    private static object? TryGetPayload(TraceEvent data, string name)
    {
        try
        {
            return data.PayloadByName(name);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
