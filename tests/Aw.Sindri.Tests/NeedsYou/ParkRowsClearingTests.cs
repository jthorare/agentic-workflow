using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The park rows clear when the instance resumes or ends (API spec section 3.6; API P11 U3), through the real projections.</summary>
[Lane(TestLane.Integration)]
public sealed class ParkRowsClearingTests
{
    private static readonly string _address = "ITM-" + new string('1', 32) + "/step/1";
    private static readonly string _other = "ITM-" + new string('1', 32) + "/review/1";

    [Fact]
    public async Task Row_clears_when_the_instance_resumes_or_ends()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "the item", repo: "agentic-workflow");

        await Fold(q, EventTypes.WorkflowStarted, Row("produce", new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk), suspension: null));
        var desk = await Kinds(q);
        await Fold(q, EventTypes.WorkflowTransition, Row("produce", frame: null, Parked()));
        var parked = await Kinds(q);
        await Fold(q, EventTypes.WorkflowTransition, Row("produce", frame: null, suspension: null));
        var resumed = await Kinds(q);
        await Fold(q, EventTypes.WorkflowTransition, Row("done", new FrameSummary("done", 2, Run: null, Child: null), suspension: null, outcome: "resolved"));
        var ended = await Kinds(q);

        Assert.Equal(["desk:"], desk);
        Assert.Equal(["park:"], parked);   // the desk task is replaced by the park
        Assert.Equal(["desk:"], resumed);
        Assert.Empty(ended);
    }

    [Fact]
    public async Task An_unavailable_park_shows_no_row_and_clears_the_desk_task_it_replaced()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "the item", repo: "agentic-workflow");

        await Fold(q, EventTypes.WorkflowStarted, Row("produce", new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk), suspension: null));
        var desk = await Kinds(q);
        await Fold(q, EventTypes.WorkflowTransition, Row("produce", frame: null, new SuspensionSummary(SuspensionKind.Park, ParkReason.Unavailable, ParkKind.Step, Escalation: null, Mirrors: null)));
        var unavailable = await Kinds(q);
        await Fold(q, EventTypes.WorkflowTransition, Row("produce", frame: null, Parked()));
        var parked = await Kinds(q);

        Assert.Equal(["desk:"], desk);
        Assert.Empty(unavailable);   // nobody is asked: the park is in health, not in needs-you
        Assert.Equal(["park:"], parked);   // a later park a human must answer still shows
    }

    [Fact]
    public async Task An_item_ended_by_its_workflow_voids_what_it_waited_on_as_a_human_close_does()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "the item", repo: "agentic-workflow");
        var ids = q.Views.Slice.Ids;
        await q.Views.Fold(EventTypes.RequestFiled, new RequestFiled(ids.New<CommandId>(), A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, ItemQueryHarness.Id('1'), RepoName.From("agentic-workflow"), "widen the scope"), ItemQueryHarness.Id('1'));
        await Fold(q, EventTypes.WorkflowStarted, Row("produce", new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk), suspension: null));
        var waiting = await Ids(q);

        await Fold(q, EventTypes.WorkflowTransition, Row("done", new FrameSummary("done", 2, Run: null, Child: null), suspension: null, outcome: "resolved"));

        Assert.Contains("decision:" + A.FirstDecisionId.Value, waiting, StringComparer.Ordinal);
        Assert.Empty(await Ids(q));
        var decision = (await q.Views.Views.Get<HumanDecisionRow>(ViewNames.Decisions, A.FirstDecisionId.Value, ItemQueryHarness.Token)).Value!;
        var request = (await q.Views.Views.Get<RequestRow>(ViewNames.Requests, A.FirstRequestId.Value, ItemQueryHarness.Token)).Value!;
        Assert.Equal((DecisionState.Voided, RequestState.Voided), (decision.State, request.State));
    }

    [Fact]
    public async Task Two_roots_on_one_item_each_keep_and_clear_their_own_rows()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "the item", repo: "agentic-workflow");

        await Fold(q, EventTypes.WorkflowStarted, Row("produce", new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk), suspension: null));
        await Fold(q, EventTypes.WorkflowStarted, Row("review", new FrameSummary("review", 1, "2.1", Child: null, StepRuntime.Desk), suspension: null, address: _other));
        var both = await Ids(q);
        await Fold(q, EventTypes.WorkflowTransition, Row("produce", frame: null, Parked()));   // the first root, no longer the latest, parks
        var firstParked = await Ids(q);
        await Fold(q, EventTypes.WorkflowTransition, Row("done", new FrameSummary("done", 2, Run: null, Child: null), suspension: null, outcome: "resolved"));
        var firstEnded = await Ids(q);
        await Fold(q, EventTypes.WorkflowTransition, Row("done", new FrameSummary("done", 2, Run: null, Child: null), suspension: null, outcome: "resolved", address: _other));
        var bothEnded = await Ids(q);

        Assert.Equal(["desk:" + Instance(_address), "desk:" + Instance(_other)], both);
        Assert.Equal(["desk:" + Instance(_other), "park:" + Instance(_address)], firstParked);
        Assert.Equal(["desk:" + Instance(_other)], firstEnded);
        Assert.Empty(bothEnded);
    }

    [Fact]
    public async Task A_row_stored_without_detail_leaves_the_park_row_alone()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "the item", repo: "agentic-workflow");
        await Fold(q, EventTypes.WorkflowStarted, Row("produce", new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk), suspension: null));
        await Fold(q, EventTypes.WorkflowTransition, Row("produce", frame: null, Parked()));

        await Fold(q, EventTypes.WorkflowTransition, new WorkflowEventPayload(_address, "produce", "produce", "deadline", Outcome: null, Run: null, Detail: null));

        Assert.Equal(["park:"], await Kinds(q));
    }

    private static string Instance(string address) => WorkflowInstanceId.Of(InstanceAddress.From(address)).Value;

    private static async Task<string[]> Ids(ItemQueryHarness q)
    {
        var rows = await q.Views.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, ItemQueryHarness.Token);
        return [.. rows.Value.Select(static r => r.Row.Id.Value).Order(StringComparer.Ordinal)];
    }

    private static async Task<string[]> Kinds(ItemQueryHarness q)
    {
        var rows = await q.Views.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, ItemQueryHarness.Token);
        return [.. rows.Value.Select(static r => r.Row.Id.Value[..(r.Row.Id.Value.IndexOf(':', StringComparison.Ordinal) + 1)])];
    }

    private static Task Fold(ItemQueryHarness q, string type, WorkflowEventPayload payload) => q.Views.Fold(type, payload, ItemQueryHarness.Id('1'));

    private static SuspensionSummary Parked() => new(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null);

    private static WorkflowEventPayload Row(string to, FrameSummary? frame, SuspensionSummary? suspension, string? outcome = null, string? address = null)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(address ?? _address, "from", to, "step.completed", Outcome: null, Run: null, detail);
    }
}
