using System.Globalization;
using System.Text;
using NetWatcher.ViewModels;

namespace NetWatcher.Services.Export;

/// <summary>Exports the current view as RFC 4180-style CSV.</summary>
public static class CsvExporter
{
    private static readonly string[] Columns =
    {
        "Time", "Process", "Pid", "Domain", "IP", "Port", "Protocol", "State", "Source", "LastActivity",
    };

    private static readonly char[] QuotedCharacters = { ',', '"', '\n', '\r' };
    private static readonly char[] FormulaLeaders = { '=', '+', '-', '@', '\t' };

    /// <summary>Builds the CSV text for the visible rows (inbound rows are excluded).</summary>
    public static string ToCsv(IEnumerable<ConnectionRowViewModel> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Columns));

        foreach (var row in rows.Where(row => !row.Inbound))
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Escape(row.TimeIso),
                Escape(row.ProcessName),
                Escape(row.Pid.ToString(CultureInfo.InvariantCulture)),
                Escape(row.DomainDisplay),
                Escape(row.Ip),
                Escape(row.Port.ToString(CultureInfo.InvariantCulture)),
                Escape(row.ProtocolName),
                Escape(row.State),
                Escape(row.SourceName),
                Escape(row.LastActivityIso),
            }));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Quotes a field when it contains a comma, quote, or line break, and
    /// neutralizes the leading characters Excel treats as a formula.
    /// </summary>
    public static string Escape(string field)
    {
        field ??= string.Empty;

        if (field.Length > 0 && Array.IndexOf(FormulaLeaders, field[0]) >= 0)
        {
            field = "'" + field;
        }

        if (field.IndexOfAny(QuotedCharacters) < 0)
        {
            return field;
        }

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
