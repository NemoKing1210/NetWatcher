using System.Net;
using System.Net.Sockets;

namespace NetWatcher.Utils;

/// <summary>Address and port helpers shared by the ETW parsers and the table reader.</summary>
public static class IpHelpers
{
    /// <summary>Builds an address from exactly 4 (IPv4) or 16 (IPv6) bytes.</summary>
    public static IPAddress ParseAddressBytes(byte[] buffer, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (length is not (4 or 16))
        {
            throw new ArgumentException("Address length must be 4 (IPv4) or 16 (IPv6).", nameof(length));
        }

        if (offset < 0 || offset + length > buffer.Length)
        {
            throw new ArgumentException("Address range lies outside the buffer.", nameof(offset));
        }

        var bytes = new byte[length];
        Array.Copy(buffer, offset, bytes, 0, length);
        return new IPAddress(bytes);
    }

    /// <summary>
    /// Converts a port read from a Windows connection table (network byte order)
    /// into host byte order.
    /// </summary>
    public static ushort NetworkPortToHost(ushort port) =>
        unchecked((ushort)IPAddress.NetworkToHostOrder((short)port));

    /// <summary>
    /// True for addresses that can meaningfully be looked up in public DNS:
    /// not loopback, private, link-local, CGNAT, unspecified, or multicast.
    /// </summary>
    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        // A dual-stack socket can report ::ffff:a.b.c.d; treat it as IPv4 so the
        // private-range checks and the PTR cache agree with the IPv4 observation.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            var first = bytes[0];
            var second = bytes[1];

            return first switch
            {
                0 => false,                                          // unspecified / this network
                10 => false,                                         // RFC 1918
                100 when second is >= 64 and <= 127 => false,        // CGNAT 100.64.0.0/10
                127 => false,                                        // loopback
                169 when second == 254 => false,                     // link-local
                172 when second is >= 16 and <= 31 => false,         // RFC 1918
                192 when second == 168 => false,                     // RFC 1918
                >= 224 => false,                                     // multicast / reserved
                _ => true,
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();

            return !address.IsIPv6LinkLocal
                && !address.IsIPv6SiteLocal
                && !address.IsIPv6Multicast
                && !IPAddress.IPv6Loopback.Equals(address)
                && !IPAddress.IPv6Any.Equals(address)
                && (bytes[0] & 0xFE) != 0xFC          // unique local fc00::/7
                && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8); // 2001:db8::/32
        }

        return false;
    }
}
