using System.Net;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>Which addresses a source host may have: public ones, unless the profile marks the host as intended.</summary>
internal static class HostPolicy
{
    // Loopback, unspecified and "this network", private, shared (CGNAT), link-local, IETF protocol, multicast and reserved (IPv4);
    // unspecified, loopback, unique-local, link-local, site-local and multicast (IPv6).
    private static readonly (byte[] Prefix, int Bits)[] _blocked =
    [
        .. new[] { "0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16", "172.16.0.0/12", "192.0.0.0/24", "192.168.0.0/16", "224.0.0.0/3", "::/128", "::1/128", "fc00::/7", "fe80::/10", "fec0::/10", "ff00::/8" }
            .Select(static cidr =>
            {
                var slash = cidr.IndexOf('/', StringComparison.Ordinal);
                return (IPAddress.Parse(cidr[..slash]).GetAddressBytes(), int.Parse(cidr[(slash + 1)..], System.Globalization.CultureInfo.InvariantCulture));
            }),
    ];

    /// <summary>True when <paramref name="address"/> is a public unicast address. An IPv4-mapped IPv6 address counts as its IPv4 address.</summary>
    /// <param name="address">A resolved address.</param>
    /// <returns>Whether it is public.</returns>
    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var bytes = (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).GetAddressBytes();
        return !_blocked.Any(block => block.Prefix.Length == bytes.Length && Within(bytes, block.Prefix, block.Bits));
    }

    private static bool Within(byte[] address, byte[] prefix, int bits)
    {
        var whole = bits / 8;
        var rest = bits % 8;
        return address.AsSpan(0, whole).SequenceEqual(prefix.AsSpan(0, whole))
            && (rest == 0 || ((address[whole] ^ prefix[whole]) >> (8 - rest)) == 0);
    }
}
