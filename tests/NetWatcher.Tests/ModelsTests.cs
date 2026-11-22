using NetWatcher.Models;

namespace NetWatcher.Tests;

public sealed class ModelsTests
{
    [Fact]
    public void ProcessKey_DistinctWhenStartTimeDiffers()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var first = new ProcessKey(1234, start);
        var reusedPid = new ProcessKey(1234, start.AddSeconds(1));

        Assert.NotEqual(first, reusedPid);
    }
}
