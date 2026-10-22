using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Events;

/// <summary>
/// The ledger row of a workflow transition as the event stream serves it, pinned as a golden for the frame a run opens (actor, attempt, max attempts; each omitted
/// when null). The detail is built through the real <see cref="WorkflowEventDetail.WithRunOpening"/> and written by the event factory and the public projection, so the
/// UI's fixtures can follow the served shape. AW_GOLDEN_UPDATE=1 rewrites a file.
/// </summary>
public sealed class WorkflowEventFrameGoldenTests
{
    public static TheoryData<string> Cases =>
    [
        "block-run-attempt-3-of-3",
        "nothing-open",
    ];

    private static string Of(SliceHarness h, string name)
    {
        var (summary, after) = name switch
        {
            "block-run-attempt-3-of-3" => (
                new FrameSummary("produce", Entry: 3, Run: "3.1", Child: null, StepRuntime.Block),
                FrameFixture.Instance(new Frame("produce", 3, new OpenRun(RunToken.For(3, 1), StepRuntime.Block, Launch: null, Task: null), Child: null)) with
                {
                    Loops = new Dictionary<string, LoopState>(StringComparer.Ordinal) { ["fix"] = new LoopState(3, 3, Last: null) },
                }),
            "nothing-open" => (
                new FrameSummary("done", Entry: 4, Run: null, Child: null),
                FrameFixture.Instance(new Frame("done", 4, Run: null, Child: null))),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
        var delta = new WorkflowEventDelta(summary, Suspension: null, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null);
        var detail = new WorkflowEventDetail(TriggerKey.For(FrameFixture.Id, "run:3.1:" + new string('f', 64)), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta).WithRunOpening(after, FrameFixture.Recipe);
        var payload = new WorkflowEventPayload(A.FirstItemId.Value + "/step/1", "produce", summary.State, "step.completed", Outcome: null, Run: "3.1", detail);

        var entry = h.Events.Entry(EventTypes.WorkflowTransition, payload, "key", A.FirstItemId.Value);
        var row = new Core.Ledger.LedgerEvent(Sequence: 1, Epoch: 1, entry.Envelope, entry.IdempotencyKey, entry.Item);
        return ApiGolden.Reindent(PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: false).Value!.Payload.GetRawText());
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Cases))]
    public void The_served_transition_equals_its_golden(string name) =>
        Golden.Match(Of(new SliceHarness(), name), typeof(WorkflowEventFrameGoldenTests).Assembly, $"Golden/ledger/workflow.transition-{name}.json", Normalization.None);
}
