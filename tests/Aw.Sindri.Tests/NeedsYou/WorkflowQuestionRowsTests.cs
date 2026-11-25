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

/// <summary>The needs-you row of an 'ask_owner' question: it waits for an answer and clears when the question is answered, its node ends or its run ends.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowQuestionRowsTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@3:12");
    private static readonly WorkflowRunId _other = WorkflowRunId.From("nightly@3:13");
    private static readonly NodeId _ask = NodeId.From("ask");
    private readonly ViewsHarness _v = new();
    private long _sequence;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LedgerEvent Event<TPayload>(string type, TPayload payload) => Recorded.Of(_v.Slice.Events, type, payload, ++_sequence);

    private LedgerEvent Question(WorkflowRunId run, IReadOnlyList<string>? choices = null) =>
        Event(EventTypes.WorkflowQuestion, new WorkflowQuestionPayload(run, _ask, "Ship the release?", choices ?? [], Default: "no", TimeoutAt: null, "service:workflow"));

    private async Task<IReadOnlyList<NeedsYouRowRecord>> Rows() => (await _v.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, Token)).Value;

    [Fact]
    public async Task A_question_is_a_needs_you_row_naming_its_run_and_node_with_the_way_to_answer()
    {
        var asked = Question(_run, ["yes", "no"]);
        await _v.Apply([asked]);

        var row = Assert.Single(await Rows());

        Assert.Equal("question:nightly@3:12:ask", row.Row.Id.Value);
        Assert.Equal(NeedsYouKind.Question, row.Row.Kind);
        Assert.Equal(AnswerKind.Text, row.Row.AnswerKind);
        Assert.Equal(new NeedsYouSubject(SubjectKind.WorkflowRun, SubjectId.From("nightly.3:12")), row.Row.Subject);
        Assert.Equal("Ship the release?", row.Row.Title);
        Assert.Contains("yes", row.Row.Reason, StringComparison.Ordinal);
        Assert.Contains("sindri answer <run>/<node>", row.Row.Cli, StringComparison.Ordinal);
        Assert.Contains("nightly@3:12/ask", row.Row.Reason, StringComparison.Ordinal);
        Assert.Equal(asked.Sequence, row.Sequence);
    }

    private LedgerEvent ItemQuestion(int item) =>
        Event(EventTypes.WorkflowQuestion, new WorkflowQuestionPayload(_run, _ask, $"Ship item {item.ToString(System.Globalization.CultureInfo.InvariantCulture)}?", [], Default: null, TimeoutAt: null, "service:workflow") { Item = item, BatchWorkflow = "nightly@3:12/each/1" });

    [Fact]
    public async Task Two_items_of_one_node_are_two_rows_that_name_their_item_and_keep_the_waiting_workflow()
    {
        await _v.Apply([ItemQuestion(1), ItemQuestion(2)]);

        var rows = (await Rows()).OrderBy(static r => r.Row.Id.Value, StringComparer.Ordinal).ToList();

        Assert.Equal(["question:nightly@3:12:ask:1", "question:nightly@3:12:ask:2"], rows.Select(static r => r.Row.Id.Value), StringComparer.Ordinal);
        Assert.Contains("nightly@3:12/ask item 2", rows[1].Row.Reason, StringComparison.Ordinal);
        Assert.Contains("--item <item>", rows[1].Row.Cli, StringComparison.Ordinal);
        Assert.All(rows, static r => Assert.Equal("nightly@3:12/each/1", r.BatchWorkflow));
    }

    [Fact]
    public async Task Answering_one_item_clears_only_that_items_row()
    {
        await _v.Apply([ItemQuestion(1), ItemQuestion(2), Event(EventTypes.WorkflowAnswered, new WorkflowAnsweredPayload(_run, _ask, "yes", "owner") { Item = 1 })]);

        var row = Assert.Single(await Rows());

        Assert.Equal("question:nightly@3:12:ask:2", row.Row.Id.Value);
    }

    [Fact]
    public async Task An_item_whose_node_finishes_clears_only_that_items_row()
    {
        await _v.Apply([ItemQuestion(1), ItemQuestion(2), Event(EventTypes.WorkflowNodeFinished, new WorkflowNodeFinishedPayload(_run, _ask, 2, "succeeded", Truncated: false, "service:workflow"))]);

        var row = Assert.Single(await Rows());

        Assert.Equal("question:nightly@3:12:ask:1", row.Row.Id.Value);
    }

    [Fact]
    public async Task Answering_clears_only_that_question()
    {
        await _v.Apply([Question(_run), Question(_other), Event(EventTypes.WorkflowAnswered, new WorkflowAnsweredPayload(_run, _ask, "yes", "owner"))]);

        var row = Assert.Single(await Rows());

        Assert.Equal("question:nightly@3:13:ask", row.Row.Id.Value);
    }

    [Fact]
    public async Task The_row_clears_when_its_node_finishes_by_timeout()
    {
        await _v.Apply([Question(_run), Event(EventTypes.WorkflowNodeFinished, new WorkflowNodeFinishedPayload(_run, _ask, 0, "succeeded", Truncated: false, "service:workflow"))]);

        Assert.Empty(await Rows());
    }

    [Fact]
    public async Task The_row_clears_when_its_node_fails()
    {
        await _v.Apply([Question(_run), Question(_other), Event(EventTypes.WorkflowNodeFailed, new WorkflowNodeFailedPayload(_run, _ask, 0, "boom", "service:workflow"))]);

        var row = Assert.Single(await Rows());

        Assert.Equal("question:nightly@3:13:ask", row.Row.Id.Value);
    }

    [Theory]
    [InlineData("finished")]
    [InlineData("cancelled")]
    public async Task The_row_clears_when_its_run_ends(string how)
    {
        var end = how == "finished"
            ? Event(EventTypes.WorkflowRunFinished, new WorkflowRunFinishedPayload(_run, "failed", Error: "boom", "service:workflow", WorkflowRunMode.Live))
            : Event(EventTypes.WorkflowRunCancelled, new WorkflowRunCancelledPayload(_run, "owner stopped it", "human", WorkflowRunMode.Live));
        await _v.Apply([Question(_run), Question(_other), end]);

        var row = Assert.Single(await Rows());

        Assert.Equal("question:nightly@3:13:ask", row.Row.Id.Value);
    }

    [Fact]
    public async Task A_question_does_not_clear_a_note_of_the_same_run()
    {
        var note = Event(EventTypes.WorkflowNotify, new WorkflowNotifyPayload(_run, NodeId.From("notify"), "Drifted", Body: null, "service:workflow"));
        await _v.Apply([note, Question(_run), Event(EventTypes.WorkflowAnswered, new WorkflowAnsweredPayload(_run, _ask, "yes", "owner"))]);

        var row = Assert.Single(await Rows());

        Assert.Equal(NeedsYouKind.WorkflowNotify, row.Row.Kind);
    }
}
