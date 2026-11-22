using NetWatcher.Models;
using NetWatcher.Services.Etw;

namespace NetWatcher.Tests;

/// <summary>
/// The exact QueryResults string form is pinned by the calibration probe in
/// Task 11; these tests use the documented textual forms ("type: 1 IP;" and
/// bare addresses). The parser is expected to be adjusted to the probe output.
/// </summary>
public sealed class DnsEventParserTests
{
    private static readonly ProcessKey Process = new(4242, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Parse_Ipv4Sockaddr()
    {
        var record = DnsEventParser.Parse(
            Process,
            DateTime.UtcNow,
            "example.com",
            DnsStatus.Success,
            "type:  1 1.2.3.4;");

        Assert.NotNull(record);
        Assert.Equal("example.com", record.QueryName);
        Assert.Equal("1.2.3.4", Assert.Single(record.Addresses).ToString());
    }

    [Fact]
    public void Parse_Ipv6Sockaddr()
    {
        var record = DnsEventParser.Parse(
            Process,
            DateTime.UtcNow,
            "example.com",
            DnsStatus.Success,
            "type:  28 IP: 2001:db8::1;");

        Assert.NotNull(record);
        Assert.Equal("2001:db8::1", Assert.Single(record.Addresses).ToString());
    }

    [Fact]
    public void Parse_MultipleIps()
    {
        var record = DnsEventParser.Parse(
            Process,
            DateTime.UtcNow,
            "example.com",
            DnsStatus.Success,
            "type:  1 1.2.3.4; type:  1 5.6.7.8;");

        Assert.NotNull(record);
        Assert.Equal(2, record.Addresses.Length);
        Assert.Contains(record.Addresses, a => a.ToString() == "1.2.3.4");
        Assert.Contains(record.Addresses, a => a.ToString() == "5.6.7.8");
    }

    [Fact]
    public void Parse_TruncatedResultsReturnsEmptyAddresses()
    {
        var addresses = DnsEventParser.ParseQueryResults("type:  1 1.2.3");

        Assert.Empty(addresses);
    }

    [Fact]
    public void Parse_ReturnsNullWhenNoUsableAddress()
    {
        Assert.Null(DnsEventParser.Parse(Process, DateTime.UtcNow, "example.com", DnsStatus.Success, string.Empty));
        Assert.Null(DnsEventParser.Parse(Process, DateTime.UtcNow, "example.com", DnsStatus.NoResult, "garbage"));
    }

    [Fact]
    public void Parse_KeepsFailedLookupWithoutAddresses()
    {
        var record = DnsEventParser.Parse(
            Process,
            DateTime.UtcNow,
            "missing.example",
            DnsStatus.Error,
            "garbage");

        Assert.NotNull(record);
        Assert.Empty(record.Addresses);
        Assert.Equal(DnsStatus.Error, record.Status);
    }
}
