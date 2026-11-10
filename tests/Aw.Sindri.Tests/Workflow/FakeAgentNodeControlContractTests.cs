using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class FakeAgentNodeControlContractTests : AgentNodeControlContract
{
    protected override ValueTask<IAgentNodeControl> Create() => ValueTask.FromResult<IAgentNodeControl>(new FakeAgentNodeControl());

    protected override ValueTask Wait(IAgentNodeControl subject, WorkflowRunId run, NodeId node)
    {
        ((FakeAgentNodeControl)subject).Wait(node);
        return ValueTask.CompletedTask;
    }
}
