using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>The connect step of an 'http' node refuses an address that is the box itself (agent-first PR 9).</summary>
[Lane(TestLane.Unit)]
public sealed class GraphHttpHandlerTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.8.9.10")]
    [InlineData("::1")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("169.254.169.254")]
    [InlineData("fe80::1")]
    public void A_loopback_unspecified_or_link_local_address_is_refused(string address) =>
        Assert.False(GraphHttpHandler.MayConnect(IPAddress.Parse(address)));

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("10.0.0.5")]
    [InlineData("2606:2800:220:1:248:1893:25c8:1946")]
    public void A_public_or_private_address_is_allowed(string address) =>
        Assert.True(GraphHttpHandler.MayConnect(IPAddress.Parse(address)));
}
