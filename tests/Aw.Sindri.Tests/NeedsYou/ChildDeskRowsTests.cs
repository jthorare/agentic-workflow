using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>A child instance's open desk run gets its own 'desk:' row (q65 A1), opening the child's questions, and clears when the run closes, the child parks or it ends.</summary>
[Lane(TestLane.Integration)]
public sealed class ChildDeskRowsTests
{
    private static readonly string _root = "ITM-" + new string('1', 32) + "/line/1";
    private static readonly string _child = _root + "/scoping.1";
    private static readonly FrameSummary _open = new("produce", 1, "1.1", Child: null, StepRuntime.Desk);

    [Fact]
    public async Task A_childs_open_desk_run_opens_a_row_of_its_own_that_opens_its_questions()
    {
        var q = await Started();

        await Fold(q, EventTypes.WorkflowTransition, Row(_child, _open));

        var row = Assert.Single(await Rows(q)).Row;
        Assert.Equal(("desk:" + Instance(_child), NeedsYouKind.Park, ItemQueryHarness.Id('1')), (row.Id.Value, row.Kind, row.Item!.Value));
        Assert.Equal(new NeedsYouOpen("workflow/" + _child, "Answer the question"), row.Open);
    }

    [Fact]
    public async Task The_row_clears_when_the_childs_run_closes()
    {
        var q = await Started();
        await Fold(q, EventTypes.WorkflowTransition, Row(_child, _open));

        await Fold(q, EventTypes.WorkflowTransition, Row(_child, new FrameSummary("next", 2, Run: null, Child: null)));

        Assert.Empty(await Rows(q));
    }

    [Fact]
    public async Task The_row_clears_when_the_child_parks_or_ends()
    {
        var q = await Started();
        await Fold(q, EventTypes.WorkflowTransition, Row(_child, _open));
        await Fold(q, EventTypes.WorkflowTransition, Row(_child, frame: null, Parked()));
        var parked = (await Rows(q)).Select(static r => r.Row.Id.Value).ToArray();
        await Fold(q, EventTypes.WorkflowTransition, Row(_child, _open));
        await Fold(q, EventTypes.WorkflowTransition, Row(_child, new FrameSummary("done", 3, Run: null, Child: null), suspension: null, outcome: "resolved"));

        Assert.Equal(["park:" + Instance(_root)], parked);
        Assert.Empty(await Rows(q));
    }

    [Fact]
    public async Task A_child_row_that_says_nothing_of_its_run_leaves_the_desk_row_standing()
    {
        var q = await Started();
        await Fold(q, EventTypes.WorkflowTransition, Row(_child, _open));

        await Fold(q, EventTypes.WorkflowTransition, Row(_child, frame: null));

        Assert.Equal("desk:" + Instance(_child), Assert.Single(await Rows(q)).Row.Id.Value);
    }

    [Fact]
    public async Task A_child_park_still_feeds_the_roots_park_row_beside_a_child_desk_row()
    {
        var q = await Started();
        var other = _root + "/phase.p1.1";
        await Fold(q, EventTypes.WorkflowTransition, Row(_child, _open));

        await Fold(q, EventTypes.WorkflowTransition, Row(other, frame: null, Parked()));

        var rows = await Rows(q);
        Assert.Equal(["desk:" + Instance(_child), "park:" + Instance(_root)], rows.Select(static r => r.Row.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var park = rows.Single(static r => r.Row.Id.Value.StartsWith("park:", StringComparison.Ordinal)).Row;
        Assert.Equal((other, new NeedsYouOpen("workflow/" + other, "Answer in the child")), (park.Child, park.Open));
    }

    private static async Task<ItemQueryHarness> Started()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "the item", repo: "agentic-workflow");
        await Fold(q, EventTypes.WorkflowStarted, Row(_root, new FrameSummary("running", 1, Run: null, Child: null)));
        return q;
    }

    private static string Instance(string address) => WorkflowInstanceId.Of(InstanceAddress.From(address)).Value;

    private static async Task<IReadOnlyList<NeedsYouRowRecord>> Rows(ItemQueryHarness q) =>
        (await q.Views.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, ItemQueryHarness.Token)).Value;

    private static Task Fold(ItemQueryHarness q, string type, WorkflowEventPayload payload) => q.Views.Fold(type, payload, ItemQueryHarness.Id('1'));

    private static SuspensionSummary Parked() => new(SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Step, Escalation: null, Mirrors: null);

    private static WorkflowEventPayload Row(string address, FrameSummary? frame, SuspensionSummary? suspension = null, string? outcome = null)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(address, "from", "to", "step.completed", Outcome: null, Run: null, detail);
    }
}
