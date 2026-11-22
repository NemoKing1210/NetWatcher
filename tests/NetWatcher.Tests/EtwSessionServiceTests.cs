using NetWatcher.Services.Etw;

namespace NetWatcher.Tests;

public sealed class EtwSessionServiceTests
{
    [Theory]
    [InlineData(47873, 443)]   // 0xBB01 on the wire is port 443
    [InlineData(20675, 50000)] // 0x50C3 on the wire is port 50000
    [InlineData(0, 0)]
    public void KernelPortToHost_ConvertsNetworkByteOrder(int raw, int expected)
    {
        Assert.Equal(expected, EtwSessionService.KernelPortToHost(raw));
    }

    [Theory]
    [InlineData(-1)]   // field missing from the payload
    [InlineData(70000)] // out of range: not a port
    public void KernelPortToHost_KeepsInvalidValuesInvalid(int raw)
    {
        Assert.Equal(-1, EtwSessionService.KernelPortToHost(raw));
    }

    [Fact]
    public void NormalizeTimestamp_ConvertsLocalTimeToUtc()
    {
        var local = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

        var utc = EtwSessionService.NormalizeTimestamp(local);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(local.ToUniversalTime(), utc);
    }

    [Fact]
    public void NormalizeTimestamp_LeavesUtcUnchanged()
    {
        var utc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(utc, EtwSessionService.NormalizeTimestamp(utc));
    }
}
