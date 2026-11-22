using System.Net;
using NetWatcher.Services.ReverseDns;

namespace NetWatcher.Tests;

public sealed class ReverseDnsResolverTests
{
    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; private set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;

        public void Advance(TimeSpan by) => Now += by;
    }

    [Fact]
    public async Task ResolveAsync_SkipsPrivateAddresses()
    {
        var calls = 0;
        var resolver = new ReverseDnsResolver(
            (_, _) =>
            {
                calls++;
                return Task.FromResult<string?>("should.not.be.used");
            },
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await resolver.ResolveAsync(IPAddress.Parse("192.168.1.1"));

        Assert.Null(result);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ResolveAsync_CachesWithin24h()
    {
        var calls = 0;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var resolver = new ReverseDnsResolver(
            (_, _) =>
            {
                calls++;
                return Task.FromResult<string?>("host.example");
            },
            clock);

        var first = await resolver.ResolveAsync(IPAddress.Parse("8.8.8.8"));
        clock.Advance(TimeSpan.FromHours(23));
        var second = await resolver.ResolveAsync(IPAddress.Parse("8.8.8.8"));

        Assert.Equal("host.example", first);
        Assert.Equal("host.example", second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ResolveAsync_ExpiresAfter24h()
    {
        var calls = 0;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var resolver = new ReverseDnsResolver(
            (_, _) =>
            {
                calls++;
                return Task.FromResult<string?>("host.example");
            },
            clock);

        await resolver.ResolveAsync(IPAddress.Parse("8.8.8.8"));
        clock.Advance(TimeSpan.FromHours(25));
        await resolver.ResolveAsync(IPAddress.Parse("8.8.8.8"));

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ResolveAsync_TimesOut()
    {
        var resolver = new ReverseDnsResolver(
            async (_, _) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                return "late.example";
            },
            new FakeTimeProvider(DateTimeOffset.UtcNow),
            TimeSpan.FromMilliseconds(100));

        var result = await resolver.ResolveAsync(IPAddress.Parse("8.8.8.8"));

        Assert.Null(result);
    }
}
