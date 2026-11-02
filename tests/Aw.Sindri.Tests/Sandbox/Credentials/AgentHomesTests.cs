using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Tests.Sandbox.Credentials;

/// <summary>The one formula every SB-8 part uses for a separate-login agent home.</summary>
public sealed class AgentHomesTests
{
    [Fact, Trait("Lane", "unit")]
    public void An_agent_home_sits_under_agent_homes_named_by_its_target() =>
        Assert.Equal("/home/owner/.agentic-workflow/sindri-app/agent-homes/claude-code", AgentHomes.For("/home/owner/.agentic-workflow/sindri-app/", AgentId.From("claude-code")));
}
