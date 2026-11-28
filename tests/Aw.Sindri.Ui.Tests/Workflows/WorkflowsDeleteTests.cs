using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Delete on a workflow's runs (Workflows page spec §4.9, #3306, #3312): one confirm with three forms, sent once, the list read again after, and the notice says what was kept and freed.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsDeleteTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // morning-check, served off (list-two serves it active), opened
    private async Task<WorkflowsPageViewModel> OpenedOff()
    {
        ServeState(active: null);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    private async Task<WorkflowsPageViewModel> OpenedActive()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    private void ServeState(int? active)
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["activeRevision"] = active;
        _f.ServeList(list);
    }

    private static string NameOf(WorkflowsPageViewModel page) => page.Rows.First(r => string.Equals(r.Slug, Slug, StringComparison.Ordinal)).NameText;

    [Fact]
    public async Task Delete_is_offered_on_an_open_workflow_and_not_on_the_list()
    {
        var list = await _f.Shown(Token);
        Assert.False(list.CanDelete);

        await list.OpenWorkflow(Slug, Token);

        Assert.True(list.CanDelete);
    }

    [Fact]
    public async Task Delete_on_an_off_workflow_opens_the_confirm_that_names_it_and_sends_nothing()
    {
        var page = await OpenedOff();

        page.RequestDelete();

        Assert.Equal((true, true, false, false), (page.IsConfirmingDelete, page.IsDeleteOffForm, page.IsDeleteActiveForm, page.IsDeleteInFlightForm));
        Assert.Equal($"Delete {NameOf(page)}? Its runs stay readable on the Runs page. Its resources are deleted. You can't undo this.", page.DeleteConfirmText);
        Assert.Empty(_f.Server.DeletedWorkflows);
    }

    [Fact]
    public async Task Cancel_closes_the_confirm_and_deletes_nothing()
    {
        var page = await OpenedOff();
        page.RequestDelete();

        page.CancelDelete();

        Assert.False(page.IsConfirmingDelete);
        Assert.Empty(_f.Server.DeletedWorkflows);
    }

    [Fact]
    public async Task An_active_workflow_shows_the_pause_form_and_its_Delete_sends_nothing()
    {
        var page = await OpenedActive();

        page.RequestDelete();
        await page.ConfirmDelete(Token);

        Assert.Equal((true, true, false), (page.IsConfirmingDelete, page.IsDeleteActiveForm, page.IsDeleteOffForm));
        Assert.Equal($"{NameOf(page)} is active. Pause it, then delete it.", page.DeleteConfirmText);
        Assert.Empty(_f.Server.DeletedWorkflows);
    }

    [Fact]
    public async Task Pause_in_the_active_form_pauses_and_shows_the_first_form_in_place()
    {
        var page = await OpenedActive();
        page.RequestDelete();
        ServeState(active: null);

        await page.PauseFromDelete(Token);

        Assert.Equal([Slug], _f.Server.PausedWorkflows);
        Assert.Equal((true, true, false), (page.IsConfirmingDelete, page.IsDeleteOffForm, page.IsDeleteActiveForm));
        Assert.Empty(_f.Server.DeletedWorkflows);
    }

    [Fact]
    public async Task Delete_sends_once_returns_to_the_list_reads_it_again_and_says_what_was_kept()
    {
        var page = await OpenedOff();
        var reads = _f.Server.WorkflowsCalls;
        page.RequestDelete();

        await page.ConfirmDelete(Token);

        Assert.Equal([Slug], _f.Server.DeletedWorkflows);
        Assert.Equal(reads + 1, _f.Server.WorkflowsCalls);
        Assert.Equal((false, true, false), (page.IsConfirmingDelete, page.IsList, page.IsDeleting));
        Assert.Equal("Deleted morning-check. 0 runs kept, 0.0 MB freed", page.DeletedNoticeText);
        Assert.True(page.HasDeletedNotice);
    }

    [Fact]
    public async Task The_notice_names_the_resources_that_stayed()
    {
        var page = await OpenedOff();
        _f.Server.DeleteWorkflowAnswer = new RouteAnswer(HttpStatusCode.OK,
            """{"slug":"morning-check","archivedAt":"2026-01-01T00:00:00Z","runsKept":12,"bytesFreed":1200000,"resourcesStayed":["node:morning-check/a"]}""");
        page.RequestDelete();

        await page.ConfirmDelete(Token);

        Assert.Equal("Deleted morning-check. 12 runs kept, 1.2 MB freed. These resources stayed: node:morning-check/a; run 'sindri resources purge <owner>' to try again", page.DeletedNoticeText);
    }

    [Fact]
    public async Task A_second_press_while_the_call_is_on_the_way_does_nothing()
    {
        var page = await OpenedOff();
        _f.Server.DeleteWorkflowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.RequestDelete();

        var first = page.ConfirmDelete(Token);
        await page.ConfirmDelete(Token);

        Assert.Equal((true, "Deleting…"), (page.IsDeleting, page.DeleteYesText));
        _f.Server.DeleteWorkflowGate.SetResult();
        await first;
        _ = Assert.Single(_f.Server.DeletedWorkflows);
    }

    [Fact]
    public async Task A_delete_that_succeeds_still_returns_to_the_list_when_Cancel_came_before_the_answer()
    {
        var page = await OpenedOff();
        var reads = _f.Server.WorkflowsCalls;
        _f.Server.DeleteWorkflowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.RequestDelete();

        var sent = page.ConfirmDelete(Token);
        page.CancelDelete();
        _f.Server.DeleteWorkflowGate.SetResult();
        await sent;

        Assert.Equal((true, false, true), (page.IsList, page.IsConfirmingDelete, page.HasDeletedNotice));
        Assert.Equal(reads + 1, _f.Server.WorkflowsCalls);
    }

    [Fact]
    public async Task Cancel_does_nothing_while_the_delete_is_on_the_way()
    {
        var page = await OpenedOff();
        _f.Server.DeleteWorkflowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.RequestDelete();

        var sent = page.ConfirmDelete(Token);
        page.CancelDelete();

        Assert.True(page.IsConfirmingDelete);
        _f.Server.DeleteWorkflowGate.SetResult();
        await sent;
    }

    [Theory]
    [InlineData("SND-GRAPH-008")]
    [InlineData("SND-GRAPH-011")]
    public async Task A_delete_of_a_workflow_that_is_already_gone_returns_to_the_list_and_reads_it_again(string code)
    {
        var page = await OpenedOff();
        var reads = _f.Server.WorkflowsCalls;
        _f.Server.DeleteWorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, code, "No such workflow.");
        page.RequestDelete();

        await page.ConfirmDelete(Token);

        Assert.Equal((true, false, false), (page.IsList, page.IsConfirmingDelete, page.IsDeleting));
        Assert.Equal(reads + 1, _f.Server.WorkflowsCalls);
    }

    [Fact]
    public async Task A_run_still_going_turns_the_confirm_into_the_in_flight_form_that_opens_the_run()
    {
        var page = await OpenedOff();
        _f.Server.DeleteWorkflowAnswer = RouteAnswer.Problem("workflows-slug", "archive-in-flight");
        page.RequestDelete();

        await page.ConfirmDelete(Token);

        Assert.Equal((true, true, false), (page.IsConfirmingDelete, page.IsDeleteInFlightForm, page.IsDeleteOffForm));
        Assert.Equal($"A run of {NameOf(page)} is still going. Delete it after the run ends.", page.DeleteConfirmText);
        Assert.Equal("morning-check@1:1", page.DeleteRunId);

        await page.OpenDeleteRun(Token);

        Assert.Equal((true, false), (page.IsRun, page.IsConfirmingDelete));
    }

    [Fact]
    public async Task A_114_from_Sindri_turns_the_confirm_into_the_pause_form()
    {
        var page = await OpenedOff();
        _f.Server.DeleteWorkflowAnswer = RouteAnswer.Problem("workflows-slug", "archive-active");
        page.RequestDelete();
        ServeState(active: 1);   // the re-read shows what the 114 says: the workflow is active

        await page.ConfirmDelete(Token);

        Assert.Equal((true, true), (page.IsConfirmingDelete, page.IsDeleteActiveForm));
        Assert.False(page.IsList);
        Assert.True(page.CanPause);
    }

    [Fact]
    public async Task Another_refusal_is_said_in_words_and_the_confirm_stays()
    {
        var page = await OpenedOff();
        _f.Server.DeleteWorkflowAnswer = RouteAnswer.Refused();
        page.RequestDelete();

        await page.ConfirmDelete(Token);

        Assert.StartsWith("Couldn't delete the workflow. ", page.DeleteNoteText, StringComparison.Ordinal);
        Assert.Equal((true, true, false), (page.IsConfirmingDelete, page.HasDeleteNote, page.IsDeleting));
    }

    [Fact]
    public async Task The_confirm_closes_when_another_workflow_opens()
    {
        var page = await OpenedOff();
        page.RequestDelete();

        await page.OpenWorkflow("weekly-report", Token);

        Assert.False(page.IsConfirmingDelete);
    }
}
