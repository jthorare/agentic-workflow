using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class RunAgentLaunchTests
{
    [Fact]
    public void The_branch_is_wf_slug_run_and_the_session_name_uses_the_launch_id()
    {
        var run = WorkflowRunId.From("contract-drift@3:12");
        var launch = LaunchId.From("LCH-0123456789abcdef0123456789abcdef");

        var branch = RunAgentNames.Branch(run);
        var session = LaunchSessionName.For(launch);

        Assert.Equal("wf/contract-drift/r3-12", branch);
        Assert.Equal("snd-LCH-0123456789abcdef0123456789abcdef", session);
        Assert.DoesNotContain(':', branch);
        Assert.DoesNotContain('@', branch);
        Assert.DoesNotContain(':', session);
        Assert.DoesNotContain('@', session);
    }
}
