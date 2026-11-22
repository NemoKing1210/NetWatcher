using System.Net;
using System.Net.NetworkInformation;
using NetWatcher.Models;
using NetWatcher.Services.Export;
using NetWatcher.ViewModels;

namespace NetWatcher.Tests;

public sealed class CsvExporterTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ConnectionRowViewModel Row(
        string ip = "1.2.3.4",
        bool inbound = false,
        string domain = "example.com",
        DomainOrigin origin = DomainOrigin.Dns) =>
        new(
            new ConnectionRecord(
                new ProcessKey(7, T0.AddDays(-1)),
                Protocol.Tcp,
                IPAddress.Parse(ip),
                443,
                50000,
                TcpState.Established,
                ConnectionSource.Etw,
                T0,
                T0.AddSeconds(5),
                inbound),
            new DomainBinding(domain, origin),
            "chrome");

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    public void Escape_QuotesWhenNeeded(string input, string expected)
    {
        Assert.Equal(expected, CsvExporter.Escape(input));
    }

    [Fact]
    public void ToCsv_HeaderMatchesSpecColumns()
    {
        var csv = CsvExporter.ToCsv(Array.Empty<ConnectionRowViewModel>());

        Assert.StartsWith("Time,Process,Pid,Domain,IP,Port,Protocol,State,Source,LastActivity", csv);
    }

    [Fact]
    public void ToCsv_ExcludesInboundRows()
    {
        var rows = new[] { Row(ip: "1.2.3.4"), Row(ip: "5.6.7.8", inbound: true) };

        var csv = CsvExporter.ToCsv(rows);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Contains("1.2.3.4", csv);
        Assert.DoesNotContain("5.6.7.8", csv);
    }

    [Fact]
    public void ToCsv_RowsMatchInputValues()
    {
        var csv = CsvExporter.ToCsv(new[] { Row() });
        var line = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)[1];

        Assert.Contains("chrome", line);
        Assert.Contains("example.com", line);
        Assert.Contains("1.2.3.4", line);
        Assert.Contains("443", line);
        Assert.Contains("TCP", line);
    }
}
