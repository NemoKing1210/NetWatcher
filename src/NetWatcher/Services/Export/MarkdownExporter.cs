using System.Globalization;
using System.Text;
using NetWatcher.ViewModels;

namespace NetWatcher.Services.Export;

/// <summary>Exports the current view as a GitHub-flavoured Markdown table.</summary>
public static class MarkdownExporter
{
    private static readonly string[] Columns =
    {
        "Time", "Process", "PID", "Domain", "IP", "Port", "Protocol", "State", "Source", "Last activity",
    };

    /// <summary>Builds the Markdown table for the visible rows (inbound rows are excluded).</summary>
    public static string ToMarkdown(IEnumerable<ConnectionRowViewModel> rows)
    {
        var builder = new StringBuilder();
        builder.Append("| ").AppendJoin(" | ", Columns).AppendLine(" |");
        builder.Append('|').AppendJoin('|', Columns.Select(_ => " --- ")).AppendLine("|");

        foreach (var row in rows.Where(row => !row.Inbound))
        {
            builder.Append("| ").AppendJoin(" | ", new[]
            {
                Cell(row.TimeIso),
                Cell(row.ProcessName),
                row.Pid.ToString(CultureInfo.InvariantCulture),
                Cell(row.DomainDisplay),
                Cell(row.Ip),
                row.Port.ToString(CultureInfo.InvariantCulture),
                Cell(row.ProtocolName),
                Cell(row.State),
                Cell(row.SourceName),
                Cell(row.LastActivityIso),
            }).AppendLine(" |");
        }

        return builder.ToString();
    }

    /// <summary>Keeps a value inside one table cell: pipes and newlines cannot break the row.</summary>
    private static string Cell(string value) =>
        (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("|", "\\|")
            .Replace('\r', ' ')
            .Replace('\n', ' ');
}
