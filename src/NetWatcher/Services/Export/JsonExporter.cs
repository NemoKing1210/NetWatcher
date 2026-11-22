using System.Text.Json;
using NetWatcher.ViewModels;

namespace NetWatcher.Services.Export;

/// <summary>Exports the current view as a JSON array of connection objects.</summary>
public static class JsonExporter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Builds the JSON for the visible rows (inbound rows are excluded).</summary>
    public static string ToJson(IEnumerable<ConnectionRowViewModel> rows)
    {
        var records = rows
            .Where(row => !row.Inbound)
            .Select(row => new
            {
                time = row.Time,
                process = row.ProcessName,
                pid = row.Pid,
                domain = row.DomainDisplay,
                ip = row.Ip,
                port = row.Port,
                protocol = row.ProtocolName,
                state = row.State,
                source = row.SourceName,
                lastActivity = row.LastActivity,
            })
            .ToArray();

        return JsonSerializer.Serialize(records, Options);
    }
}
