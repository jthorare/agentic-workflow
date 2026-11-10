using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>What every <see cref="IAgentNodeControl"/> does for the node a run waits on: says which one it is, and a takeover and a handback move its held state (#2472).</summary>
public abstract class AgentNodeControlContract
{
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(WorkflowSlug.From("morning-check"), 1, 1);
    private static readonly NodeId _agent = NodeId.From("agent");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Makes the subject: a control for a run that waits on no node.</summary>
    /// <returns>The subject.</returns>
    protected abstract ValueTask<IAgentNodeControl> Create();

    /// <summary>Makes the run of <see cref="Create"/> wait on <paramref name="node"/>, held by nobody.</summary>
    /// <param name="subject">The subject from <see cref="Create"/>.</param>
    /// <param name="run">The run.</param>
    /// <param name="node">The node.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask Wait(IAgentNodeControl subject, WorkflowRunId run, NodeId node);

    /// <summary>A run that waits on no 'run_agent' node answers null.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_run_that_waits_on_no_agent_node_has_none()
    {
        var subject = await Create();

        var running = await subject.Running(_run, Token);

        Expect.True(running.IsSuccess);
        Expect.True(running.Value is null);
    }

    /// <summary>The node the run waits on is named, held by nobody.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_node_the_run_waits_on_is_named_and_free()
    {
        var subject = await Create();
        await Wait(subject, _run, _agent);

        var running = await subject.Running(_run, Token);

        Expect.True(running.IsSuccess && running.Value is not null);
        var node = running.Value!;
        Expect.True(node.Node == _agent);
        Expect.Equal(0, node.ItemIndex);
        Expect.True(!node.TakenOver);
    }

    /// <summary>A takeover holds the node, and a handback frees it again.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_takeover_holds_the_node_and_a_handback_frees_it()
    {
        var subject = await Create();
        await Wait(subject, _run, _agent);

        var taken = await subject.Takeover(_run, _agent, 0, Token);
        var held = (await subject.Running(_run, Token)).Value;
        var back = await subject.Handback(_run, _agent, 0, Token);
        var freed = (await subject.Running(_run, Token)).Value;

        Expect.True(taken.IsSuccess);
        Expect.True(held is { TakenOver: true });
        Expect.True(back.IsSuccess);
        Expect.True(freed is { TakenOver: false });
    }
}
