using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// A 'run_agent' node's launch is suspended while a person holds the node (#2471): the sandbox wall counts only unsuspended time, and a takeover pauses the node's timeout, so it must pause the wall too.
/// The rows are the real 'workflow.node.taken-over' and 'workflow.node.handed-back' rows of #2497, about the run's item.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LedgerRunSuspensionsNodeTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@3:12");
    private static readonly NodeId _agent = NodeId.From("agent");
    private static readonly NodeId _other = NodeId.From("review");

    private readonly SliceHarness _slice = new();
    private LeaseGrant? _grant;
    private int _rows;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<LedgerRunSuspensions> Subject()
    {
        _grant = await _slice.HoldLease();
        var started = new LaunchStarted(A.FirstLaunchId, A.FirstItemId, LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri", new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Opened, Code: null, Detail: null), ["tmux"], CatalogSource.Builtin, "sha256:abc", Instance: null)
        {
            Run = _run.Value,
            Node = _agent.Value,
        };
        await Append(EventTypes.LaunchStarted, started, $"launch.outcome:{A.FirstLaunchId.Value}", A.FirstItemId.Value);
        return new LedgerRunSuspensions(_slice.Store, _slice.Events);
    }

    private static async Task<bool> Suspended(LedgerRunSuspensions subject)
    {
        var answer = await subject.IsSuspended(A.FirstLaunchId, Token);
        Expect.True(answer.IsSuccess);
        return answer.Value;
    }

    // why: #2497 writes the rows about the run's item, so a run with no item writes them with none; 'onItem' false is that run
    private ValueTask TakenOver(NodeId node, int item = 0, bool onItem = true) =>
        Append(EventTypes.WorkflowNodeTakenOver, new WorkflowNodeTakenOverPayload(_run, node, item, "human:cli"), Key(EventTypes.WorkflowNodeTakenOver, node, item), onItem ? A.FirstItemId.Value : null);

    private ValueTask HandedBack(NodeId node, int item = 0, bool onItem = true) =>
        Append(EventTypes.WorkflowNodeHandedBack, new WorkflowNodeHandedBackPayload(_run, node, item, "human:cli"), Key(EventTypes.WorkflowNodeHandedBack, node, item), onItem ? A.FirstItemId.Value : null);

    [Fact]
    public async Task A_node_taken_over_suspends_its_launch()
    {
        var subject = await Subject();
        await TakenOver(_agent);

        Expect.True(await Suspended(subject));
    }

    [Fact]
    public async Task A_node_handed_back_resumes_its_launch()
    {
        var subject = await Subject();
        await TakenOver(_agent);
        await HandedBack(_agent);

        Expect.True(!await Suspended(subject));
    }

    [Fact]
    public async Task A_node_taken_over_again_after_a_handback_suspends_its_launch_again()
    {
        var subject = await Subject();
        await TakenOver(_agent);
        await HandedBack(_agent);
        await TakenOver(_agent);

        Expect.True(await Suspended(subject));
    }

    [Fact]
    public async Task Another_nodes_takeover_does_not_suspend_the_launch()
    {
        var subject = await Subject();
        await TakenOver(_other);

        Expect.True(!await Suspended(subject));
    }

    [Fact]
    public async Task A_launch_with_no_takeover_rows_is_not_suspended()
    {
        var subject = await Subject();

        Expect.True(!await Suspended(subject));
    }

    [Fact]
    public async Task A_takeover_of_another_loop_item_does_not_suspend_the_launch()
    {
        var subject = await Subject();
        await TakenOver(_agent, item: 1);

        Expect.True(!await Suspended(subject));
    }

    [Fact]
    public async Task A_run_with_no_item_taken_over_suspends_its_launch()
    {
        var subject = await Subject();
        await TakenOver(_agent, onItem: false);

        Expect.True(await Suspended(subject));
    }

    [Fact]
    public async Task A_run_with_no_item_handed_back_resumes_its_launch()
    {
        var subject = await Subject();
        await TakenOver(_agent, onItem: false);
        await HandedBack(_agent, onItem: false);

        Expect.True(!await Suspended(subject));
    }

    [Fact]
    public async Task A_nodes_takeover_does_not_suspend_a_node_whose_id_starts_with_its_id()
    {
        var subject = await Subject();
        await TakenOver(NodeId.From("agent2"), onItem: false);

        Expect.True(!await Suspended(subject));
    }

    private string Key(string type, NodeId node, int item) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{type}:{_run.Value}:{node.Value}:{item}:cmd{_rows++}");

    private async ValueTask Append<T>(string type, T payload, string key, string? item = null)
    {
        var appended = await _slice.Store.Append(_slice.Events.Entry(type, payload, key, item), _grant!, Token);   // why: Subject ran first
        Expect.True(appended.IsSuccess);
    }
}
