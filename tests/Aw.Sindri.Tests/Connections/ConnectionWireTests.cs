using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class ConnectionWireTests
{
    [Theory]
    [InlineData(ConnectionKind.OAuth, "oauth")]
    [InlineData(ConnectionKind.Mcp, "mcp")]
    [InlineData(ConnectionKind.Rest, "rest")]
    [InlineData(ConnectionKind.Cli, "cli")]
    public void A_kind_has_its_wire_name_both_ways(ConnectionKind kind, string wire)
    {
        Assert.Equal(wire, ConnectionWire.Name(kind));
        Assert.Equal(kind, ConnectionWire.ParseKind(wire).Value);
    }

    [Theory]
    [InlineData(ConnectionStatus.Connected, "connected")]
    [InlineData(ConnectionStatus.NeedsSignIn, "needs-sign-in")]
    [InlineData(ConnectionStatus.Error, "error")]
    public void A_status_has_its_wire_name_both_ways(ConnectionStatus status, string wire)
    {
        Assert.Equal(wire, ConnectionWire.Name(status));
        Assert.Equal(status, ConnectionWire.ParseStatus(wire).Value);
    }

    [Fact]
    public void An_unknown_wire_name_is_an_error()
    {
        Assert.True(ConnectionWire.ParseKind("ldap").IsError);
        Assert.True(ConnectionWire.ParseStatus("OK").IsError);
    }
}
