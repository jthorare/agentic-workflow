using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.AnswerQuestion;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.AnswerQuestion;

/// <summary>'sindri answer': an answer reaches the run's workflow and the ledger says who gave it; an unknown, ended or bad answer is refused.</summary>
[Lane(TestLane.Unit)]
public sealed class AnswerQuestionServiceTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");
    private static readonly NodeId _ask = NodeId.From("ask");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly FakeGraphRunAnswerer _answerer = new();
    private readonly SliceHarness _h = new();
    private readonly AnswerQuestionService _service;

    public AnswerQuestionServiceTests()
    {
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new AnswerQuestionService(_runs, _answerer, _h.Store, _h.Held, _h.Events);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_answer_is_delivered_to_the_workflow_and_recorded_with_who_gave_it()
    {
        _ = await _h.HoldLease();
        var run = await Insert(1, WorkflowRunStatus.Running);

        var result = await _service.Handle(new AnswerQuestionRequest(run.Id, _ask, "ship it", Choice: null, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(("ship it", "owner"), (result.Value.Answer, result.Value.AnsweredBy));
        Assert.Equal([new GraphAnswer(run.Id, _ask, "ship it", Choice: null, "owner")], _answerer.Answers);
        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal(EventTypes.WorkflowAnswered, recorded.Envelope.Type);
        Assert.Equal($"workflow.answered:{run.Id.Value}:ask", recorded.IdempotencyKey);
        var payload = JsonNode.Parse(recorded.Envelope.Payload)!;
        Assert.Equal(("ship it", "owner"), (payload["answer"]!.GetValue<string>(), payload["by"]!.GetValue<string>()));
    }

    [Fact]
    public async Task An_agent_session_may_answer_its_own_runs_question_and_the_timeline_says_so()
    {
        _ = await _h.HoldLease();
        var run = await Insert(2, WorkflowRunStatus.Running);

        var result = await _service.Handle(new AnswerQuestionRequest(run.Id, _ask, Text: null, "yes", "session:abc123"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(("yes", "session:abc123"), (result.Value.Answer, result.Value.AnsweredBy));
        Assert.Equal("session:abc123", Assert.Single(_answerer.Answers).By);
        var payload = JsonNode.Parse(Assert.Single(_h.Store.Events).Envelope.Payload)!;
        Assert.Equal("session:abc123", payload["by"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("a", "b")]
    public async Task An_answer_has_exactly_one_of_text_and_choice(string? text, string? choice)
    {
        _ = await _h.HoldLease();
        var run = await Insert(3, WorkflowRunStatus.Running);

        var result = await _service.Handle(new AnswerQuestionRequest(run.Id, _ask, text, choice, "owner"), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_answerer.Answers);
        Assert.Empty(_h.Store.Events);
    }

    [Fact]
    public async Task An_unknown_run_is_not_found()
    {
        var result = await _service.Handle(new AnswerQuestionRequest(WorkflowRunId.Of(_slug, 1, 7), _ask, "x", Choice: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphRunNotFound.Code, result.Error.Code);
        Assert.Empty(_answerer.Answers);
    }

    [Theory]
    [InlineData(WorkflowRunStatus.Succeeded)]
    [InlineData(WorkflowRunStatus.Failed)]
    [InlineData(WorkflowRunStatus.Cancelled)]
    public async Task A_run_that_already_ended_is_refused(WorkflowRunStatus status)
    {
        var run = await Insert(4, status);

        var result = await _service.Handle(new AnswerQuestionRequest(run.Id, _ask, "x", Choice: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphRunEnded.Code, result.Error.Code);
        Assert.Empty(_answerer.Answers);
    }

    [Fact]
    public async Task A_queued_run_has_no_question_yet()
    {
        var run = await Insert(5, WorkflowRunStatus.Queued);

        var result = await _service.Handle(new AnswerQuestionRequest(run.Id, _ask, "x", Choice: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotWaiting.Code, result.Error.Code);
        Assert.Empty(_answerer.Answers);
    }

    [Fact]
    public async Task An_answer_the_workflow_refuses_is_returned_as_its_error_and_records_nothing()
    {
        _ = await _h.HoldLease();
        var run = await Insert(6, WorkflowRunStatus.Running);
        _answerer.Fail = SindriErrors.GraphNotWaiting.With("'ask' is not waiting for an answer");

        var result = await _service.Handle(new AnswerQuestionRequest(run.Id, _ask, "x", Choice: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotWaiting.Code, result.Error.Code);
        Assert.Empty(_h.Store.Events);
    }

    [Fact]
    public async Task An_answer_without_the_lease_is_refused_before_it_is_delivered()
    {
        var run = await Insert(7, WorkflowRunStatus.Running);

        var result = await _service.Handle(new AnswerQuestionRequest(run.Id, _ask, "x", Choice: null, "owner"), Token);

        Assert.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
        Assert.Empty(_answerer.Answers);
    }

    private async Task<WorkflowRun> Insert(int occurrence, WorkflowRunStatus status)
    {
        var run = new WorkflowRun(WorkflowRunId.Of(_slug, 1, occurrence), _workflow, 1, "{\"kind\":\"manual\"}", status, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), EndedAt: null);
        return (await _runs.Insert(run, Token)).Value;
    }
}
