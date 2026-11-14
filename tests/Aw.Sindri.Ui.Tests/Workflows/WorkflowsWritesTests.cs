using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Run now on a workflow's runs and Cancel run on a run's detail (#2258 PR 17 part 2a): each is sent once, shows what Sindri answered in words, and reads again.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsWritesTests
{
    private const string NewRun = "morning-check@1:4";

    private const string Other = "weekly-report";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> OnRuns()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    [Fact]
    public async Task Run_now_posts_once_reads_the_runs_again_and_opens_the_new_run()
    {
        _f.Server.WorkflowRunAnswers[NewRun] = RouteAnswer.Api("workflow-runs-run", "running");
        var page = await OnRuns();
        var reads = _f.Server.WorkflowRunsCalls;

        await page.RunNow(Token);

        Assert.Equal([Slug], _f.Server.StartedWorkflows);
        Assert.Equal(reads + 1, _f.Server.WorkflowRunsCalls);
        Assert.Equal((true, NewRun, false), (page.IsRun, page.Run.Id, page.Runs.IsStarting));
        Assert.Equal("Run now", page.Runs.RunNowText);
    }

    [Fact]
    public async Task Run_now_on_a_workflow_that_is_off_says_the_refusal_in_words_and_stays_on_the_runs()
    {
        var page = await OnRuns();
        _f.Server.StartWorkflowRunAnswer = RouteAnswer.Problem("workflows-slug-runs", "not-active");

        await page.RunNow(Token);

        Assert.Equal((true, string.Empty), (page.IsRuns, page.Run.Id));
        Assert.StartsWith("Couldn't start a run. 'morning-check' has no active revision; activate one first", page.Runs.NoticeText, StringComparison.Ordinal);
        Assert.True(page.Runs.HasNotice);
        Assert.Equal((false, false), (page.Runs.IsStarting, page.Runs.CanPressRunNow));   // Sindri said it is off: Run now turns off too
    }

    [Fact]
    public async Task A_refusal_that_lands_after_another_workflow_opened_shows_nothing_under_it()
    {
        _f.Server.WorkflowRunsAnswers[Other] = RouteAnswer.Api("workflows-slug-runs", "list-empty");
        var page = await OnRuns();
        _f.Server.StartWorkflowRunGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.StartWorkflowRunAnswer = RouteAnswer.Problem("workflows-slug-runs", "not-active");

        var pressing = page.RunNow(Token);
        await page.OpenWorkflow(Other, Token);
        _f.Server.StartWorkflowRunGate.SetResult();
        await pressing;

        Assert.Equal((Other, string.Empty, false), (page.Runs.Slug, page.Runs.NoticeText, page.Runs.HasNotice));
        Assert.True(page.Runs.CanPressRunNow);   // the other workflow's refusal doesn't turn this one's Run now off
    }

    [Fact]
    public async Task Run_now_stays_off_until_the_runs_read_lands()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.WorkflowGates[Slug] = gate;
        var page = await _f.Shown(Token);

        var opening = page.OpenWorkflow(Slug, Token);
        Assert.False(page.Runs.CanPressRunNow);
        await page.RunNow(Token);
        gate.SetResult();
        await opening;

        Assert.Empty(_f.Server.StartedWorkflows);
        Assert.True(page.Runs.CanPressRunNow);
    }

    [Fact]
    public async Task When_Sindri_does_not_answer_Run_now_says_the_run_may_have_started_and_reads_the_runs_again()
    {
        var page = await OnRuns();
        var reads = _f.Server.WorkflowRunsCalls;
        _f.Server.GoDown();

        await page.RunNow(Token);

        Assert.StartsWith("Sindri didn't answer, so the run may have started; check the list before you press Run now again.", page.Runs.NoticeText, StringComparison.Ordinal);
        Assert.Equal(reads + 1, _f.Server.WorkflowRunsCalls);
        Assert.Equal((true, false), (page.IsRuns, page.Runs.IsStarting));
    }

    [Fact]
    public async Task A_queued_run_says_so_and_is_not_opened()
    {
        var page = await OnRuns();
        _f.Server.StartWorkflowRunAnswer = RouteAnswer.SpecShaped("workflows-slug-runs", "queued");

        await page.RunNow(Token);

        Assert.Equal((true, "The run was queued: another run of this workflow is still going"), (page.IsRuns, page.Runs.NoticeText));
    }

    [Fact]
    public async Task A_second_Run_now_while_a_run_is_in_flight_says_so_in_one_line_and_opens_that_run()
    {
        var page = await OnRuns();   // the runs list has morning-check@1:3 running
        _f.Server.StartWorkflowRunAnswer = RouteAnswer.SpecShaped("workflows-slug-runs", "skipped");

        await page.RunNow(Token);

        Assert.Equal((true, "morning-check@1:3", "A run is already in flight."), (page.IsRun, page.Run.Id, page.Run.NoticeText));
    }

    [Fact]
    public async Task A_skipped_run_with_no_run_in_flight_to_open_keeps_the_skipped_note()
    {
        var page = await OnRuns();
        _f.ServeRuns("list-empty");
        _f.Server.StartWorkflowRunAnswer = RouteAnswer.SpecShaped("workflows-slug-runs", "skipped");

        await page.RunNow(Token);

        Assert.Equal((true, "The run was skipped: another run of this workflow is still going"), (page.IsRuns, page.Runs.NoticeText));
    }

    [Fact]
    public async Task A_second_press_while_the_call_is_on_the_way_sends_nothing()
    {
        var page = await OnRuns();
        _f.Server.StartWorkflowRunGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.StartWorkflowRunAnswer = RouteAnswer.SpecShaped("workflows-slug-runs", "skipped");

        var first = page.RunNow(Token);
        Assert.True(page.Runs.IsStarting);
        Assert.Equal(("Starting…", false), (page.Runs.RunNowText, page.Runs.CanPressRunNow));
        await page.RunNow(Token);
        _f.Server.StartWorkflowRunGate.SetResult();
        await first;

        Assert.Equal([Slug], _f.Server.StartedWorkflows);
        Assert.False(page.Runs.IsStarting);
    }

    [Fact]
    public async Task A_workflow_that_is_off_cannot_be_run_from_the_runs_level()
    {
        _f.Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Problem("workflows-slug-runs", "not-active");
        var page = await OnRuns();

        await page.RunNow(Token);

        Assert.Equal((false, false), (page.Runs.CanRun, page.Runs.CanPressRunNow));
        Assert.Empty(_f.Server.StartedWorkflows);
    }

    [Fact]
    public async Task Cancel_shows_only_while_the_run_is_running_or_waiting()
    {
        var finished = await _f.OpenedRun(Token);
        Assert.False(finished.CanCancel);

        _f.ServeRun("running");
        var running = await _f.OpenedRun(Token);
        Assert.Equal((true, true, "Cancel run"), (running.CanCancel, running.CanPressCancel, running.CancelText));
    }

    [Fact]
    public async Task Cancel_posts_once_says_it_was_sent_and_reads_the_run_again()
    {
        _f.ServeRun("running");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);
        var reads = _f.Server.WorkflowRunCalls;

        await page.CancelRun(Token);

        Assert.Equal([RunId], _f.Server.CancelledWorkflowRuns);
        Assert.Equal(reads + 1, _f.Server.WorkflowRunCalls);
        Assert.Equal((Labels.WfCancelSent, false), (page.Run.NoticeText, page.Run.IsCancelling));
    }

    [Fact]
    public async Task Cancel_of_a_run_that_already_ended_says_so_in_words_and_reads_the_run_again()
    {
        _f.ServeRun("running");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);
        _f.Server.CancelWorkflowRunAnswer = RouteAnswer.Problem("workflow-runs-run-cancel", "already-ended");
        _f.ServeRun("finished");

        await page.CancelRun(Token);

        Assert.Equal("That run already ended, so there was nothing to cancel.", page.Run.NoticeText);
        Assert.Equal(("✓ Succeeded", false), (page.Run.ChipGlyph + " " + page.Run.ChipWord, page.Run.CanCancel));
    }

    [Fact]
    public async Task Cancel_that_is_refused_for_another_reason_shows_the_message_and_does_not_read_again()
    {
        _f.ServeRun("running");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);
        var reads = _f.Server.WorkflowRunCalls;
        _f.Server.CancelWorkflowRunAnswer = RouteAnswer.Problem("workflow-runs-run-cancel", "not-found");

        await page.CancelRun(Token);

        Assert.StartsWith("Couldn't cancel the run. No run 'morning-check@1:1'", page.Run.NoticeText, StringComparison.Ordinal);
        Assert.Equal(reads, _f.Server.WorkflowRunCalls);
    }

    [Fact]
    public async Task A_failed_runs_read_offers_Try_again_that_reads_again()
    {
        _ = _f.Server.WorkflowRunsAnswers.Remove(Slug);
        var page = await OnRuns();
        Assert.Equal(("Try again", true), (page.Runs.RetryText, page.Runs.IsFailed));

        _f.ServeRuns("list-three");
        await page.RetryRuns(Token);

        Assert.Equal((false, 3), (page.Runs.IsFailed, page.Runs.Rows.Count));
    }

    [Fact]
    public async Task A_failed_runs_read_hides_its_failure_line_while_it_reads_again()
    {
        _ = _f.Server.WorkflowRunsAnswers.Remove(Slug);
        var page = await OnRuns();
        Assert.True(page.Runs.IsFailed);
        var gate = new TaskCompletionSource();
        _f.Server.WorkflowGates[Slug] = gate;
        _f.ServeRuns("list-three");

        var retrying = page.RetryRuns(Token);

        Assert.Equal((true, false), (page.Runs.IsReading, page.Runs.IsFailed));
        gate.SetResult();
        await retrying;
        Assert.False(page.Runs.IsFailed);
    }

    [Fact]
    public async Task A_failed_run_read_hides_its_failure_line_while_it_reads_again()
    {
        _ = _f.Server.WorkflowRunAnswers.Remove(RunId);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);
        Assert.True(page.Run.IsFailed);
        var gate = new TaskCompletionSource();
        _f.Server.WorkflowRunGates[RunId] = gate;
        _f.ServeRun("finished");

        var retrying = page.RetryRun(Token);

        Assert.Equal((true, false), (page.Run.IsReading, page.Run.IsFailed));
        gate.SetResult();
        await retrying;
        Assert.False(page.Run.IsFailed);
    }

    [Fact]
    public async Task A_failed_run_read_offers_Try_again_that_reads_again()
    {
        _ = _f.Server.WorkflowRunAnswers.Remove(RunId);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);
        Assert.Equal(("Try again", true), (page.Run.RetryText, page.Run.IsFailed));

        _f.ServeRun("finished");
        await page.RetryRun(Token);

        Assert.Equal((false, "✓ Succeeded"), (page.Run.IsFailed, page.Run.ChipGlyph + " " + page.Run.ChipWord));
    }

    [Theory]
    [InlineData("succeeded", "Ok", false)]
    [InlineData("running", "Ok", false)]
    [InlineData("waiting", "Attn", false)]
    [InlineData("failed", "Fail", true)]
    [InlineData("cancelled", "Neutral", false)]
    [InlineData("skipped", "Neutral", false)]
    [InlineData("queued", "Neutral", false)]
    public void A_status_has_a_tone_besides_its_glyph_and_word(string status, string tone, bool error)
    {
        var row = new RunRowViewModel(new WorkflowRunRowView("a@1:1", 1, status, "manual", Now, EndedAt: null, Error: null), _f.Clock);

        Assert.Equal((tone, error), (row.Tone.ToString(), row.IsError));
        Assert.NotEmpty(row.ChipGlyph);   // a glyph and a word
        Assert.NotEmpty(row.ChipWord);
    }

    [Theory]
    [InlineData("succeeded", "Ok", false)]
    [InlineData("waiting", "Attn", false)]
    [InlineData("failed", "Fail", true)]
    [InlineData("skipped", "Neutral", false)]
    public void A_node_runs_chip_has_a_tone_besides_its_glyph_and_word(string status, string tone, bool error)
    {
        var run = new NodeRunViewModel(new NodeRunView("n", 0, status, Input: null, Output: null, Truncated: false, Error: null), 1);

        Assert.Equal((tone, error), (run.Tone.ToString(), run.IsError));
    }

    [Theory]
    [InlineData(1, "ok")]
    [InlineData(null, "neutral")]
    public void A_workflows_chip_is_ok_only_while_it_is_on(int? active, string tone)
    {
        var row = new WorkflowRowViewModel(new WorkflowRowView("a", "A", active, 1, "owner", Now), _f.Clock);

        Assert.Equal((tone, false), (row.ChipToneWord, row.IsError));
    }
}
