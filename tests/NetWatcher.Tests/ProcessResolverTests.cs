using System.Diagnostics;
using NetWatcher.Models;
using NetWatcher.Services.Connections;

namespace NetWatcher.Tests;

public sealed class ProcessResolverTests
{
    [Fact]
    public void GetOrAdd_CachesAndKeepsAfterProcessExit()
    {
        using var process = Process.Start("cmd.exe", "/c ping -n 3 127.0.0.1 > nul");
        Assert.NotNull(process);

        var resolver = new ProcessResolver();

        var first = resolver.GetOrAdd(process.Id);
        process.WaitForExit();

        var second = resolver.GetOrAdd(process.Id);

        Assert.Same(first, second);
        Assert.Equal(process.Id, second.Key.Pid);
    }

    [Fact]
    public void GetCached_ReturnsEntryForSameKey()
    {
        var resolver = new ProcessResolver();

        var resolved = resolver.GetOrAdd(Environment.ProcessId);

        Assert.Same(resolved, resolver.GetCached(resolved.Key));
    }

    [Fact]
    public void GetCached_ReturnsNullWhenStartTimeDiffers()
    {
        var resolver = new ProcessResolver();
        var resolved = resolver.GetOrAdd(Environment.ProcessId);

        var reusedPid = new ProcessKey(resolved.Key.Pid, resolved.Key.StartTimeUtc.AddSeconds(1));

        Assert.Null(resolver.GetCached(reusedPid));
    }
}
