using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The Workflows page's list and a workflow's runs (#2258 PR 17 part 1): the rows in words with glyph and word for the state, who changed each workflow, the served empty cases and the way back.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsPageViewModelTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_list_shows_each_workflow_with_its_state_revision_and_who_changed_it()
    {
        var page = await _f.Shown(Token);

        Assert.Equal(2, page.Rows.Count);
        var (active, off) = (page.Rows[0], page.Rows[1]);
        Assert.Equal(("Morning check", "● Active", string.Empty, "Changed by owner, 5m ago"), (active.NameText, active.ChipGlyph + " " + active.ChipWord, active.RevisionText, active.ChangedText));
        Assert.Equal(("Weekly report", "○ Off", string.Empty), (off.NameText, off.ChipGlyph + " " + off.ChipWord, off.RevisionText));
        Assert.Equal("Morning check, active, changed by owner, 5m ago", active.Name);
        Assert.Equal("2 workflows · 1 active", page.Head);
        Assert.Equal((false, true, false, false), (page.IsReading, page.IsList, page.IsRuns, page.IsRun));
    }

    [Fact]
    public async Task A_session_that_changed_a_workflow_is_shown_as_text()
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["changedBy"] = "session:abc";
        _f.ServeList(list);

        var page = await _f.Shown(Token);

        Assert.Equal("Changed by session:abc, 5m ago", page.Rows[0].ChangedText);
    }

    [Fact]
    public async Task A_workflow_whose_active_revision_is_not_the_newest_says_both()
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["latestRevision"] = 3;
        _f.ServeList(list);

        var page = await _f.Shown(Token);

        Assert.Equal("Revision 1 active, 3 newest", page.Rows[0].RevisionText);
    }

    [Fact]
    public async Task No_workflows_yet_says_so()
    {
        _f.ServeList(Golden("workflows", "list-empty"));

        var page = await _f.Shown(Token);

        Assert.Equal((true, "No workflows yet.", false), (page.IsEmpty, page.EmptyText, page.IsReading));
    }

    [Fact]
    public async Task The_empty_state_offers_a_New_workflow_action_with_the_form_closed_and_names_the_agent_command()
    {
        _f.ServeList(Golden("workflows", "list-empty"));

        var page = await _f.Shown(Token);

        Assert.Equal("New workflow", page.NewWorkflowText);
        Assert.False(page.IsCreating);
        Assert.Equal(("Agents can make one with", "sindri wf create"), (page.AgentRouteLead, page.AgentRouteCommand));
    }

    [Fact]
    public async Task The_head_is_hidden_when_empty_and_counts_when_there_are_workflows()
    {
        _f.ServeList(Golden("workflows", "list-empty"));
        var empty = await _f.Shown(Token);
        var some = await new WorkflowsFixture().Shown(Token);

        Assert.Equal((false, string.Empty), (empty.HasHead, empty.Head));
        Assert.Equal((true, "2 workflows · 1 active"), (some.HasHead, some.Head));
    }

    [Fact]
    public async Task A_failed_first_read_says_so_with_nothing_to_show_and_offers_Try_again()
    {
        _f.Server.Refusal = ("SND-API-500", "it broke");

        var page = await _f.Shown(Token);

        Assert.True(page.IsFailed);
        Assert.StartsWith("Couldn't read the workflows just now.", page.FailedText, StringComparison.Ordinal);
        Assert.Equal(("Try again", false), (page.RetryText, page.IsReading));
    }

    [Fact]
    public async Task Opening_a_workflow_shows_its_runs_newest_first_with_glyph_and_word_and_trigger()
    {
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal((false, true, false), (page.IsList, page.IsRuns, page.IsRun));
        Assert.Equal(("Morning check", "1 running · 3 runs"), (page.Runs.Title, page.Runs.Head));
        Assert.Equal(["morning-check@1:3", "morning-check@1:2", "morning-check@1:1"], page.Runs.Rows.Select(r => r.Id), StringComparer.Ordinal);
        Assert.Equal(["● Running", "✕ Failed", "✓ Succeeded"], page.Runs.Rows.Select(r => r.ChipGlyph + " " + r.ChipWord), StringComparer.Ordinal);
        Assert.Equal(["Manual", "Schedule", "Manual"], page.Runs.Rows.Select(r => r.TriggerText), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_failed_run_shows_why_and_a_running_run_says_when_it_started()
    {
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        var (running, failed) = (page.Runs.Rows[0], page.Runs.Rows[1]);
        Assert.Equal(("Started 2m ago", false), (running.WhenText, running.HasError));
        Assert.Equal(("node 'agent' failed", true), (failed.ErrorText, failed.HasError));
    }

    [Fact]
    public async Task A_workflow_that_never_ran_says_so()
    {
        _f.ServeRuns("list-empty");
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal((true, "No runs yet."), (page.Runs.IsEmpty, page.Runs.EmptyText));
    }

    [Fact]
    public async Task A_workflow_that_is_off_says_it_has_no_runs_instead_of_an_error()
    {
        _f.Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Problem("workflows-slug-runs", "not-active");
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal((true, false, "This workflow is off, so it has no runs."), (page.Runs.IsEmpty, page.Runs.IsFailed, page.Runs.EmptyText));
    }

    [Fact]
    public async Task The_runs_header_says_how_big_the_shown_revision_is_and_who_made_the_workflow()
    {
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal("Revision 1 of 2 · 2 steps · created by owner", page.Runs.DetailText);
    }

    [Fact]
    public async Task An_unknown_workflow_is_a_failed_read_with_the_served_reason()
    {
        var page = await _f.Shown(Token);

        await page.OpenWorkflow("nope", Token);

        Assert.True(page.Runs.IsFailed);
        Assert.Contains("No workflow 'nope'", page.Runs.FailedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Back_goes_up_one_level_at_a_time()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);
        Assert.True(page.IsRun);

        page.Back();
        Assert.Equal((false, true, false), (page.IsList, page.IsRuns, page.IsRun));

        page.Back();
        Assert.Equal((true, false, false), (page.IsList, page.IsRuns, page.IsRun));
    }

    [Fact]
    public async Task Leaving_the_page_returns_it_to_the_list()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);

        page.Leave();

        Assert.True(page.IsList);
    }

    [Fact]
    public async Task Opening_a_workflow_after_a_failed_one_does_not_show_the_old_failure_while_reading()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow("nope", Token);
        Assert.True(page.Runs.IsFailed);
        var gate = new TaskCompletionSource();
        _f.Server.WorkflowGates[Slug] = gate;

        var opening = page.OpenWorkflow(Slug, Token);

        Assert.Equal((false, string.Empty, true), (page.Runs.IsFailed, page.Runs.FailedText, page.Runs.IsReading));
        gate.SetResult();
        await opening;
        Assert.Equal((false, 3), (page.Runs.IsFailed, page.Runs.Rows.Count));
    }

    [Fact]
    public async Task Opening_a_workflow_after_one_that_is_off_does_not_show_the_off_line_while_reading()
    {
        _f.Server.WorkflowRunsAnswers["weekly-report"] = RouteAnswer.Problem("workflows-slug-runs", "not-active");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow("weekly-report", Token);
        Assert.True(page.Runs.IsEmpty);
        var gate = new TaskCompletionSource();
        _f.Server.WorkflowGates[Slug] = gate;

        var opening = page.OpenWorkflow(Slug, Token);

        Assert.Equal((false, string.Empty, true), (page.Runs.IsEmpty, page.Runs.EmptyText, page.Runs.IsReading));
        gate.SetResult();
        await opening;
        Assert.Equal((false, 3), (page.Runs.IsEmpty, page.Runs.Rows.Count));
    }
}
