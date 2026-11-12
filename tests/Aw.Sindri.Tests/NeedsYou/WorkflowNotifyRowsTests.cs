using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The needs-you row of a graph 'notify' node: a note waits until its run ends, and folding the same events again changes nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowNotifyRowsTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@3:12");
    private static readonly WorkflowRunId _other = WorkflowRunId.From("nightly@3:13");
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly NodeId _notify = NodeId.From("notify");
    private readonly ViewsHarness _v = new();
    private long _sequence;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LedgerEvent Event<TPayload>(string type, TPayload payload) => Recorded.Of(_v.Slice.Events, type, payload, ++_sequence);

    private LedgerEvent Notify(WorkflowRunId run, string title = "Drifted", string? body = "2 contracts changed") =>
        Event(EventTypes.WorkflowNotify, new WorkflowNotifyPayload(run, _notify, title, body, "service:workflow"));

    private async Task<IReadOnlyList<NeedsYouRowRecord>> Rows() => (await _v.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, Token)).Value;

    [Fact]
    public async Task A_notify_note_is_a_needs_you_row_naming_its_run_and_node()
    {
        var notify = Notify(_run);
        await _v.Apply([notify]);

        var row = Assert.Single(await Rows());

        Assert.Equal("workflow-notify:nightly@3:12:notify", row.Row.Id.Value);
        Assert.Equal(NeedsYouKind.WorkflowNotify, row.Row.Kind);
        Assert.Equal(new NeedsYouSubject(SubjectKind.WorkflowRun, SubjectId.From("nightly.3:12")), row.Row.Subject);
        Assert.Equal("Drifted", row.Row.Title);
        Assert.Equal("2 contracts changed", row.Row.Reason);
        Assert.Equal(notify.Sequence, row.Sequence);
    }

    [Fact]
    public async Task A_note_without_a_body_is_a_row_with_the_title_only()
    {
        await _v.Apply([Notify(_run, "Done", body: null)]);

        var row = Assert.Single(await Rows());

        Assert.Equal("Done", row.Row.Title);
    }

    [Theory]
    [InlineData("finished")]
    [InlineData("cancelled")]
    public async Task The_row_clears_when_its_run_ends(string how)
    {
        var end = how == "finished"
            ? Event(EventTypes.WorkflowRunFinished, new WorkflowRunFinishedPayload(_run, "succeeded", Error: null, "service:workflow", WorkflowRunMode.Live))
            : Event(EventTypes.WorkflowRunCancelled, new WorkflowRunCancelledPayload(_run, "owner stopped it", "human", WorkflowRunMode.Live));
        await _v.Apply([Notify(_run), Notify(_other), end]);

        var row = Assert.Single(await Rows());

        Assert.Equal("workflow-notify:nightly@3:13:notify", row.Row.Id.Value);   // only the ended run's row went
    }

    [Fact]
    public async Task Folding_the_same_events_again_leaves_the_same_rows()
    {
        var events = new[] { Notify(_run), Event(EventTypes.WorkflowRunStarted, new WorkflowRunStartedPayload(_other, _workflow, 3, "{}", "human", WorkflowRunMode.Live)) };
        await _v.Apply(events);
        var first = await Rows();

        await _v.Apply(events);

        Assert.Equal(first.Select(static r => (r.Row.Id.Value, r.Sequence)), (await Rows()).Select(static r => (r.Row.Id.Value, r.Sequence)));
    }
}
