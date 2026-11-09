using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.AgentNodeControl;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.AgentNodeControl;

/// <summary>
/// POST /sindri/workflow-runs/{run}/nodes/{node}/takeover and /handback (#2472): a person or an agent session takes a 'run_agent' node over and hands it back. A call with no caller is refused,
/// A human and an agent session may both do it (agent-first D2). A node the run does not wait on is refused,, a handback of a node nobody holds and a run that is unknown or ended.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AgentNodeControlServiceTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(_slug, 1, 1);
    private static readonly NodeId _agent = NodeId.From("agent");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly FakeAgentNodeControl _control = new();

    public AgentNodeControlServiceTests()
    {
        _runs = new InMemoryWorkflowRunStore(_clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_takeover_of_the_node_the_run_waits_on_holds_it_and_answers_taken_over()
    {
        await Insert(WorkflowRunStatus.Running);
        _control.Wait(_agent);

        var result = await TakeOver(A.Caller().Human().Build());

        Assert.True(result.IsSuccess);
        Assert.Equal((_run, _agent, 0, "taken_over"), (result.Value.Run, result.Value.Node, result.Value.ItemIndex, result.Value.Agent));
        Assert.Equal([$"takeover:{_run.Value}:agent:0"], _control.Calls);
    }

    [Fact]
    public async Task A_takeover_of_a_node_already_held_answers_taken_over_again()
    {
        await Insert(WorkflowRunStatus.Waiting);
        _control.Wait(_agent, takenOver: true);

        var result = await TakeOver(A.Caller().Human().Build());

        Assert.Equal("taken_over", result.Value.Agent);
    }

    [Fact]
    public async Task A_handback_of_a_held_node_frees_it_and_answers_waiting()
    {
        await Insert(WorkflowRunStatus.Running);
        _control.Wait(_agent, takenOver: true);

        var result = await HandBack(A.Caller().Human().Build());

        Assert.True(result.IsSuccess);
        Assert.Equal((_run, _agent, 0, "waiting"), (result.Value.Run, result.Value.Node, result.Value.ItemIndex, result.Value.Agent));
        Assert.Equal([$"handback:{_run.Value}:agent:0"], _control.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_agent_session_may_take_over_and_hand_back(bool takeover)
    {
        await Insert(WorkflowRunStatus.Running);
        _control.Wait(_agent, takenOver: !takeover);
        var session = A.Caller().Launched().Build();

        var result = takeover ? await TakeOver(session) : await HandBack(session);

        Assert.True(result.IsSuccess);
        Assert.Equal(takeover ? "taken_over" : "waiting", result.Value.Agent);
        _ = Assert.Single(_control.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_unknown_run_is_not_found(bool takeover)
    {
        var human = A.Caller().Human().Build();

        var code = takeover ? (await TakeOver(human)).Error.Code : (await HandBack(human)).Error.Code;

        Assert.Equal(SindriErrors.GraphRunNotFound.Code, code);
        Assert.Empty(_control.Calls);
    }

    [Theory]
    [InlineData(WorkflowRunStatus.Succeeded, true)]
    [InlineData(WorkflowRunStatus.Failed, false)]
    [InlineData(WorkflowRunStatus.Cancelled, true)]
    public async Task A_run_that_ended_has_no_node_to_control(WorkflowRunStatus status, bool takeover)
    {
        await Insert(status);
        var human = A.Caller().Human().Build();

        var code = takeover ? (await TakeOver(human)).Error.Code : (await HandBack(human)).Error.Code;

        Assert.Equal(SindriErrors.GraphRunEnded.Code, code);
        Assert.Empty(_control.Calls);
    }

    [Theory]
    [InlineData("other", 0)]
    [InlineData("agent", 1)]
    public async Task A_node_the_run_is_not_waiting_on_is_refused(string node, int itemIndex)
    {
        await Insert(WorkflowRunStatus.Running);
        _control.Wait(NodeId.From("review"), takenOver: true);
        var human = A.Caller().Human().Build();

        var taken = await TakeOver(human, NodeId.From(node), itemIndex);
        var back = await HandBack(human, NodeId.From(node), itemIndex);

        Assert.Equal(SindriErrors.GraphNodeNotWaiting.Code, taken.Error.Code);
        Assert.Equal(SindriErrors.GraphNodeNotWaiting.Code, back.Error.Code);
        Assert.Empty(_control.Calls);
    }

    [Fact]
    public async Task A_run_that_waits_on_no_agent_node_refuses_a_takeover()
    {
        await Insert(WorkflowRunStatus.Running);

        var result = await TakeOver(A.Caller().Human().Build());

        Assert.Equal(SindriErrors.GraphNodeNotWaiting.Code, result.Error.Code);
        Assert.Empty(_control.Calls);
    }

    [Fact]
    public async Task A_handback_of_a_node_nobody_holds_is_refused()
    {
        await Insert(WorkflowRunStatus.Running);
        _control.Wait(_agent);

        var result = await HandBack(A.Caller().Human().Build());

        Assert.Equal(SindriErrors.GraphNodeNotTakenOver.Code, result.Error.Code);
        Assert.Empty(_control.Calls);
    }

    [Fact]
    public async Task A_negative_item_index_is_invalid_input()
    {
        await Insert(WorkflowRunStatus.Running);
        _control.Wait(_agent);

        var result = await TakeOver(A.Caller().Human().Build(), _agent, -1);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_control.Calls);
    }

    [Fact]
    public async Task Temporal_being_down_is_the_controls_error()
    {
        await Insert(WorkflowRunStatus.Running);
        _control.Fail = SindriErrors.EngineUnavailable.With("temporal is down");
        var human = A.Caller().Human().Build();

        var taken = await TakeOver(human);
        var back = await HandBack(human);

        Assert.Equal(SindriErrors.EngineUnavailable.Code, taken.Error.Code);
        Assert.Equal(SindriErrors.EngineUnavailable.Code, back.Error.Code);
    }

    [Fact]
    public async Task A_caller_nobody_set_is_refused()
    {
        await Insert(WorkflowRunStatus.Running);
        _control.Wait(_agent);

        var result = await TakeOver(caller: null);

        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Empty(_control.Calls);
    }

    private ValueTask<Result<AgentNodeControlled>> TakeOver(CallerContext? caller, NodeId? node = null, int itemIndex = 0) =>
        new TakeOverAgentNodeService(_runs, _control, new FixedCallerContextAccessor(caller)).Handle(new TakeOverAgentNodeRequest(_run, node ?? _agent, itemIndex), Token);

    private ValueTask<Result<AgentNodeControlled>> HandBack(CallerContext? caller, NodeId? node = null, int itemIndex = 0) =>
        new HandBackAgentNodeService(_runs, _control, new FixedCallerContextAccessor(caller)).Handle(new HandBackAgentNodeRequest(_run, node ?? _agent, itemIndex), Token);

    private async Task Insert(WorkflowRunStatus status) =>
        _ = (await _runs.Insert(new WorkflowRun(_run, _workflow, 1, "{\"kind\":\"manual\"}", status, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), EndedAt: null), Token)).Value;
}
