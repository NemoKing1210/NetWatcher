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
}
