using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>
/// A park nested below a root reaches the list as the root's one 'park' row, describing the deepest parked descendant (`via: "child"`, `child`
/// its address), and clears when that descendant leaves its park, through the real projections.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class NestedParkRowsTests
{
    private static readonly string _root = "ITM-" + new string('1', 32) + "/line/1";
    private static readonly string _phase = _root + "/phase.p1.1";
    private static readonly string _build = _phase + "/build.2";

    [Fact]
    public async Task A_nested_park_shows_on_the_roots_row_pointing_at_the_descendant()
    {
        var q = await Started();

        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, Parked(ParkReason.NeedsAnswer, ParkKind.Step)));

        var row = Assert.Single(await Rows(q)).Row;
        Assert.Equal(("park:" + Instance(_root), "park · needs answer", "child", _phase), (row.Id.Value, row.Label, row.Via, row.Child));
        Assert.Equal(Instance(_root), row.Subject.Id.Value);
        Assert.Contains("it needs an answer (" + _phase + ")", row.Reason, StringComparison.Ordinal);
        Assert.Equal(("sindri workflow answer " + _phase + " --text <answer>", ActionId.From("ApplyCommand:answer-text")), (row.Cli, row.PrimaryAction));
    }

    [Fact]
    public async Task A_nested_refused_effect_park_shows_the_refusal()
    {
        var q = await Started();

        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, Parked(ParkReason.SpecConflict, ParkKind.Dispatch), reason: "SND-WF-010: fact:nextPhase must be a phase"));

        var row = Assert.Single(await Rows(q)).Row;
        Assert.Equal(("park · dispatch refused", "child", _phase), (row.Label, row.Via, row.Child));
        Assert.Contains("SND-WF-010: fact:nextPhase must be a phase", row.Reason, StringComparison.Ordinal);
        Assert.Equal(("sindri workflow answer " + _phase + " --resume", AnswerKind.Resume, ActionId.From("ApplyCommand:answer-resume")), (row.Cli, row.AnswerKind, row.PrimaryAction));
    }

    [Fact]
    public async Task The_row_clears_when_the_descendant_leaves_its_park()
    {
        var q = await Started();
        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, Parked(ParkReason.NeedsAnswer, ParkKind.Step)));

        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, suspension: null));

        Assert.Empty(await Rows(q));
    }

    [Fact]
    public async Task The_deepest_parked_descendant_is_the_child_and_the_next_shows_when_it_resumes()
    {
        var q = await Started();
        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, Parked(ParkReason.NeedsAnswer, ParkKind.Step)));
        await Fold(q, EventTypes.WorkflowTransition, Row(_build, Parked(ParkReason.NeedsAnswer, ParkKind.Step)));
        var both = Assert.Single(await Rows(q)).Row.Child;

        await Fold(q, EventTypes.WorkflowTransition, Row(_build, suspension: null));
        var phaseOnly = Assert.Single(await Rows(q)).Row.Child;

        Assert.Equal((_build, _phase), (both, phaseOnly));
    }

    [Fact]
    public async Task A_root_transition_that_does_not_park_leaves_the_nested_row_standing()
    {
        var q = await Started();
        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, Parked(ParkReason.NeedsAnswer, ParkKind.Step)));

        await Fold(q, EventTypes.WorkflowTransition, Row(_root, suspension: null));

        Assert.Equal(_phase, Assert.Single(await Rows(q)).Row.Child);
    }

    [Fact]
    public async Task The_roots_own_park_wins_and_the_nested_row_returns_when_it_resumes()
    {
        var q = await Started();
        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, Parked(ParkReason.NeedsAnswer, ParkKind.Step)));

        await Fold(q, EventTypes.WorkflowTransition, Row(_root, Parked(ParkReason.Rounds, ParkKind.Loop)));
        var own = Assert.Single(await Rows(q)).Row;
        await Fold(q, EventTypes.WorkflowTransition, Row(_root, suspension: null));
        var after = Assert.Single(await Rows(q)).Row;

        Assert.Equal((null, "park · rounds"), (own.Via, own.Label));
        Assert.Equal(_phase, after.Child);
    }

    [Fact]
    public async Task A_root_that_mirrors_a_parked_child_serves_the_childs_own_answer()
    {
        var q = await Started();
        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, Parked(ParkReason.NeedsAnswer, ParkKind.Step)));

        await Fold(q, EventTypes.WorkflowTransition, Row(_root, Mirroring(ParkReason.NeedsAnswer, _phase)));

        var row = Assert.Single(await Rows(q)).Row;
        Assert.Equal(("child", _phase, "sindri workflow answer " + _phase + " --text <answer>"), (row.Via, row.Child, row.Cli));
    }

    [Fact]
    public async Task A_deeper_park_below_a_mirroring_root_moves_its_row_to_the_deepest()
    {
        var q = await Started();
        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, Parked(ParkReason.NeedsAnswer, ParkKind.Step)));
        await Fold(q, EventTypes.WorkflowTransition, Row(_root, Mirroring(ParkReason.NeedsAnswer, _phase)));

        await Fold(q, EventTypes.WorkflowTransition, Row(_build, Parked(ParkReason.SpecConflict, ParkKind.Dispatch)));

        var row = Assert.Single(await Rows(q)).Row;
        Assert.Equal((_build, "sindri workflow answer " + _build + " --resume"), (row.Child, row.Cli));
    }

    [Fact]
    public async Task A_nested_park_does_not_block_the_roots_desk_row()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "the item", repo: "agentic-workflow");
        await Fold(q, EventTypes.WorkflowStarted, Row(_root, suspension: null, frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));

        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, Parked(ParkReason.NeedsAnswer, ParkKind.Step)));

        Assert.Equal(["desk:" + Instance(_root), "park:" + Instance(_root)], (await Rows(q)).Select(static r => r.Row.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_root_ending_clears_the_nested_row()
    {
        var q = await Started();
        await Fold(q, EventTypes.WorkflowTransition, Row(_phase, Parked(ParkReason.NeedsAnswer, ParkKind.Step)));

        await Fold(q, EventTypes.WorkflowTransition, Row(_root, suspension: null, frame: new FrameSummary("done", 2, Run: null, Child: null), outcome: "resolved"));

        Assert.Empty(await Rows(q));
    }

    private static async Task<ItemQueryHarness> Started()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "the item", repo: "agentic-workflow");
        await Fold(q, EventTypes.WorkflowStarted, Row(_root, suspension: null, frame: new FrameSummary("running", 1, Run: null, Child: null)));
        return q;
    }

    private static string Instance(string address) => WorkflowInstanceId.Of(InstanceAddress.From(address)).Value;

    private static async Task<IReadOnlyList<NeedsYouRowRecord>> Rows(ItemQueryHarness q) =>
        (await q.Views.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, ItemQueryHarness.Token)).Value;

    private static Task Fold(ItemQueryHarness q, string type, WorkflowEventPayload payload) => q.Views.Fold(type, payload, ItemQueryHarness.Id('1'));

    private static SuspensionSummary Parked(ParkReason reason, ParkKind kind) => new(SuspensionKind.Park, reason, kind, Escalation: null, Mirrors: null);

    private static SuspensionSummary Mirroring(ParkReason reason, string child) =>
        new(SuspensionKind.Park, reason, ParkKind.Step, Escalation: null, Mirrors: Instance(child), MirrorsAddress: child);

    private static WorkflowEventPayload Row(string address, SuspensionSummary? suspension, FrameSummary? frame = null, string? outcome = null, string? reason = null)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: reason, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(address, "from", "to", "step.completed", Outcome: null, Run: null, detail);
    }
}
