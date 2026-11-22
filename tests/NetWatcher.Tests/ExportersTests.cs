using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using NetWatcher.Models;
using NetWatcher.Services.Export;
using NetWatcher.ViewModels;

namespace NetWatcher.Tests;

public sealed class ExportersTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ConnectionRowViewModel Row(
        string process = "chrome",
        string ip = "1.2.3.4",
        bool inbound = false,
        string domain = "example.com") =>
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
            new DomainBinding(domain, DomainOrigin.Dns),
            process);

    [Fact]
    public void Markdown_HasHeaderAndEscapedCells()
    {
        var markdown = MarkdownExporter.ToMarkdown(new[]
        {
            Row(process: "a|b\nc"),
            Row(ip: "5.6.7.8", inbound: true),
        });

        var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("| Time | Process | PID | Domain | IP | Port | Protocol | State | Source | Last activity |", lines[0]);
        Assert.StartsWith("| --- | --- | --- |", lines[1]);
        Assert.Equal(3, lines.Length);
        Assert.Contains("a\\|b c", markdown);
        Assert.Contains("example.com", markdown);
        Assert.DoesNotContain("5.6.7.8", markdown);
    }

    [Fact]
    public void Json_IsArrayWithTypedFieldsAndExcludesInbound()
    {
        var json = JsonExporter.ToJson(new[]
        {
            Row(),
            Row(ip: "5.6.7.8", inbound: true),
        });

        using var document = JsonDocument.Parse(json);
        var records = document.RootElement;

        Assert.Equal(JsonValueKind.Array, records.ValueKind);
        Assert.Equal(1, records.GetArrayLength());

        var record = records[0];
        Assert.Equal("chrome", record.GetProperty("process").GetString());
        Assert.Equal(7, record.GetProperty("pid").GetInt32());
        Assert.Equal(443, record.GetProperty("port").GetInt32());
        Assert.Equal("example.com", record.GetProperty("domain").GetString());
        Assert.Equal("1.2.3.4", record.GetProperty("ip").GetString());
        Assert.Equal("TCP", record.GetProperty("protocol").GetString());
        Assert.Equal("Established", record.GetProperty("state").GetString());
        Assert.Equal("ETW", record.GetProperty("source").GetString());
    }
}
