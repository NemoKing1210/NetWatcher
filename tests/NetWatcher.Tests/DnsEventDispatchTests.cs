using NetWatcher.Services.Etw;

namespace NetWatcher.Tests;

public sealed class DnsEventDispatchTests
{
    [Theory]
    [InlineData("example.com", "type: 1 93.184.216.34;", DnsEventKind.Completed)]
    [InlineData("example.com", null, DnsEventKind.Recognized)]
    [InlineData("example.com", "", DnsEventKind.Recognized)]
    [InlineData("example.com", "   ", DnsEventKind.Recognized)]
    [InlineData(null, "93.184.216.34", DnsEventKind.Unknown)]
    [InlineData(null, null, DnsEventKind.Unknown)]
    [InlineData("", "", DnsEventKind.Unknown)]
    public void Classify_MapsPayloadShapeToKind(string? name, string? results, DnsEventKind expected)
    {
        Assert.Equal(expected, DnsEventDispatch.Classify(name, results));
    }

    [Fact]
    public void SeenEventIds_ReportsEachIdOnce()
    {
        var seen = new SeenEventIds();

        Assert.True(seen.TryAdd(1015));
        Assert.False(seen.TryAdd(1015));
        Assert.True(seen.TryAdd(1016));
    }
}
