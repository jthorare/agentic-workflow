using System.Net;
using Aw.Connectors.Infrastructure.GitHub;

namespace Aw.Connectors.Tests.GitHub;

public sealed class HostPolicyTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("140.82.112.5")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:50c0:8000::154")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.0.1")]
    public void Public_addresses_are_public(string address) => Assert.True(HostPolicy.IsPublic(IPAddress.Parse(address)));

    [Theory, Trait("Lane", "unit")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:127.0.0.1")]
    public void Loopback_private_link_local_and_reserved_addresses_are_not_public(string address) => Assert.False(HostPolicy.IsPublic(IPAddress.Parse(address)));
}
