using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Mcp;

namespace Aw.Sindri.Tests.RegisterMcp;

/// <summary>AW_SINDRI_APP_NO_MCP=1 is the only value that turns the automatic MCP registration off (setup-once spec §5.4).</summary>
[Lane(TestLane.Unit)]
public sealed class EnvironmentMcpSwitchTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_one_turns_it_off(string? value, bool off) =>
        Assert.Equal(off, new EnvironmentMcpSwitch(name => name == "AW_SINDRI_APP_NO_MCP" ? value : null).Off);
}
