using System.Text;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using NetWatcher.Logging;

namespace NetWatcher.Services.Etw;

/// <summary>
/// Calibration mode (--probe): subscribes to all events from the Kernel-Network
/// and DNS-Client providers for a fixed duration and logs provider, EventID,
/// EventName, every field, PID, and timestamp. The log is the source of truth
/// for the parsers' field mappings.
/// </summary>
public static class EtwProbe
{
    private static readonly Guid DnsClientGuid = new("1c95126e-7eea-49a9-a3fe-a378b03ddb4d");

    /// <summary>Runs the probe; returns a process exit code.</summary>
    public static int Run(int durationSeconds, FileLogger logger)
    {
        try
        {
            using var session = new TraceEventSession("NetWatcher-Probe-" + Guid.NewGuid().ToString("N"));
            session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
            session.EnableProvider(DnsClientGuid, TraceEventLevel.Verbose);

            session.Source.AllEvents += data => logger.Info(Format(data));

            using var stopTimer = new Timer(
                _ =>
                {
                    try
                    {
                        session.Stop();
                    }
                    catch (Exception ex)
                    {
                        logger.Warn($"Probe stop failed: {ex.Message}");
                    }
                },
                state: null,
                dueTime: TimeSpan.FromSeconds(durationSeconds),
                period: Timeout.InfiniteTimeSpan);

            logger.Info($"Probe started for {durationSeconds}s");

            session.Source.Process();

            logger.Info("Probe finished");
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error($"Probe failed: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static string Format(TraceEvent data)
    {
        var builder = new StringBuilder();
        builder.Append(data.ProviderName ?? "unknown-provider");
        builder.Append(" id=").Append(data.ID);
        builder.Append(" name=").Append(data.EventName);
        builder.Append(" pid=").Append(data.ProcessID);
        builder.Append(" time=").Append(data.TimeStamp.ToString("O"));

        foreach (var name in data.PayloadNames)
        {
            builder.Append(' ').Append(name).Append('=');
            try
            {
                builder.Append(data.PayloadByName(name));
            }
            catch (Exception)
            {
                builder.Append("<unreadable>");
            }
        }

        return builder.ToString();
    }
}
