using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Agents card's problem code stays whole when its line wraps (#4269): "SND-" never ends a line.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentProblemLineCodeTests
{
    [Fact]
    public void The_code_is_drawn_with_non_breaking_hyphens_and_served_as_is()
    {
        var line = new AgentProblemLine("Sindri can't see agent sessions on this system: disabled", "SND-SESSION-002");

        Assert.Equal("SND‑SESSION‑002", line.ShownCode, StringComparer.Ordinal);
        Assert.Equal("SND-SESSION-002", line.Code, StringComparer.Ordinal);
    }
}
