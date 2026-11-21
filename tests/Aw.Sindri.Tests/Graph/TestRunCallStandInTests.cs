using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Graph;

/// <summary>#3744: a test run's fenced 'run_workflow' call hands back a stand-in id that is no live run's id, and no run reader answers a live run for it.</summary>
[Lane(TestLane.Unit)]
public sealed class TestRunCallStandInTests
{
    private static readonly WorkflowSlug _target = WorkflowSlug.From("target");
    private static readonly WorkflowId _targetId = WorkflowId.From("WF-" + new string('2', 32));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class NeverLive : IGraphCallPort
    {
        public int Calls { get; private set; }

        public ValueTask<int> MaxDepth(CancellationToken cancellationToken) => ValueTask.FromResult(5);

        public ValueTask<Result<GraphCalled>> Start(GraphCall call, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult<Result<GraphCalled>>(new GraphCalled(WorkflowRunId.Of(call.Target, 1, 1), "started"));
        }

        public ValueTask<Result<GraphCallEnd>> Wait(WorkflowRunId run, TimeSpan timeout, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<GraphCallEnd>>(new GraphCallEnd("succeeded", Outputs: null, Error: null));
    }

    [Fact]
    public async Task A_fenced_call_in_a_test_run_returns_a_stand_in_id_that_is_not_the_targets_first_live_run()
    {
        var live = new NeverLive();
        var fenced = GraphFence.Calls(live);
        _ = GraphTestRecorder.Begin();
        try
        {
            var called = (await fenced.Start(new GraphCall(WorkflowRunId.From("caller@1:1"), NodeId.From("call"), 0, _target, Payload: null, Depth: 1), Token)).Value;

            Assert.NotEqual("target@1:1", called.Run.Value);
            Assert.True(called.Run.IsStandIn);
            Assert.Equal(0, live.Calls);
        }
        finally
        {
            GraphTestRecorder.End();
        }
    }

    [Fact]
    public void A_stand_in_id_is_a_valid_run_id_and_a_real_slug_can_never_make_one()
    {
        var standIn = WorkflowRunId.StandIn(_target);

        Assert.True(WorkflowRunId.TryFrom(standIn.Value, out var parsed));
        Assert.True(parsed.IsStandIn);
        Assert.False(WorkflowRunId.Of(_target, 1, 1).IsStandIn);
        Assert.False(WorkflowSlug.TryFrom(standIn.Value[..standIn.Value.IndexOf('@', StringComparison.Ordinal)], out _));
        Assert.True(standIn.Value.Length <= 96);
    }

    [Fact]
    public async Task Reading_the_stand_in_never_returns_the_targets_live_run()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var workflows = new InMemoryWorkflowStore(clock);
        var spec = new GraphSpec(Version: 1, Name: "target", Description: null, Concurrency: null, Resources: null, Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await workflows.Create(_targetId, _target, spec, "owner", Token)).Value;
        var at = KnownInstants.Epoch;
        _ = (await runs.Insert(new WorkflowRun(WorkflowRunId.Of(_target, 1, 1), _targetId, 1, """{"kind":"manual"}""", WorkflowRunStatus.Succeeded, Outputs: """{"a":"live"}""", Error: null, ItemId: null, at, at.AddSeconds(5)), Token)).Value;

        var result = await new GraphRunResults(workflows, runs).Read(_target, WorkflowRunId.StandIn(_target), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphResultUnavailable.Code, result.Error.Code);
    }
}
