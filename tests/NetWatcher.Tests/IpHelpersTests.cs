using System.Net;
using NetWatcher.Utils;

namespace NetWatcher.Tests;

public sealed class IpHelpersTests
{
    [Fact]
    public void ParseAddressBytes_Ipv4()
    {
        var address = IpHelpers.ParseAddressBytes(new byte[] { 8, 8, 8, 8 }, 0, 4);

        Assert.Equal("8.8.8.8", address.ToString());
    }

    [Fact]
    public void ParseAddressBytes_Ipv6()
    {
        var bytes = new byte[]
        {
            0x20, 0x01, 0x0d, 0xb8, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0x01,
        };

        var address = IpHelpers.ParseAddressBytes(bytes, 0, 16);

        Assert.Equal("2001:db8::1", address.ToString());
    }

    [Fact]
    public void ParseAddressBytes_InvalidLengthThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            IpHelpers.ParseAddressBytes(new byte[5], 0, 5));
    }

    [Fact]
    public void NetworkPortToHost_Converts443()
    {
        var networkOrder = unchecked((ushort)0xBB01);

        Assert.Equal((ushort)443, IpHelpers.NetworkPortToHost(networkOrder));
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("172.16.5.5", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.1.1", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2001:4860:4860::8888", true)]
    [InlineData("::ffff:8.8.8.8", true)]
    [InlineData("::ffff:192.168.1.1", false)]
    public void IsPublic_FiltersPrivateAndSpecial(string text, bool expected)
    {
        Assert.Equal(expected, IpHelpers.IsPublic(IPAddress.Parse(text)));
    }
}
