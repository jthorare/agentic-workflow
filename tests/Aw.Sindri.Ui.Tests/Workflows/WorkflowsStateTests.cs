using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Activate and Pause on a workflow's runs (#2258 PR 17 part 2b): each shows on the right state, is sent once, reads the list again and says a refusal in words.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsStateTests
{
    private const string Off = "weekly-report";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> Opened(string slug)
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(slug, Token);
        return page;
    }

    // the list as the next read serves it: the workflow at this index has (or has not) an active revision
    private void ServeState(int index, int? active)
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![index]!["activeRevision"] = active;
        _f.ServeList(list);
    }

    [Fact]
    public async Task Activate_shows_on_an_off_workflow_and_Pause_on_an_active_one()
    {
        var off = await Opened(Off);
        var on = await Opened(Slug);

        Assert.Equal((true, false, "Activate"), (off.CanActivate, off.CanPause, off.ActivateText));
        Assert.Equal((false, true, "Pause"), (on.CanActivate, on.CanPause, on.PauseText));
    }

    [Fact]
    public async Task Activate_and_Pause_both_show_when_an_edit_made_a_newer_revision_than_the_active_one()
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["activeRevision"] = 1;
        list["workflows"]![0]!["latestRevision"] = 2;
        _f.ServeList(list);

        var page = await Opened(Slug);

        Assert.Equal((true, true), (page.CanActivate, page.CanPause));
    }

    [Fact]
    public async Task Neither_shows_on_the_list()
    {
        var page = await _f.Shown(Token);

        Assert.Equal((false, false), (page.CanActivate, page.CanPause));
    }

    [Fact]
    public async Task Activate_posts_once_reads_the_list_again_and_swaps_the_button()
    {
        var page = await Opened(Off);
        var reads = _f.Server.WorkflowsCalls;
        ServeState(1, 1);

        await page.Activate(Token);

        Assert.Equal([Off], _f.Server.ActivatedWorkflows);
        Assert.Equal(reads + 1, _f.Server.WorkflowsCalls);
        Assert.Equal((false, true, false, false), (page.CanActivate, page.CanPause, page.IsChangingState, page.HasStateNotice));
    }

    [Fact]
    public async Task Pause_posts_once_reads_the_list_again_and_swaps_the_button()
    {
        var page = await Opened(Slug);
        ServeState(0, active: null);

        await page.Pause(Token);

        Assert.Equal([Slug], _f.Server.PausedWorkflows);
        Assert.Equal((true, false, false), (page.CanActivate, page.CanPause, page.IsChangingState));
    }

    [Fact]
    public async Task A_second_press_while_the_call_is_on_the_way_does_nothing()
    {
        var page = await Opened(Off);
        _f.Server.WorkflowStateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = page.Activate(Token);
        await page.Activate(Token);

        Assert.Equal([Off], _f.Server.ActivatedWorkflows);
        Assert.Equal((true, "Activating…", false), (page.IsChangingState, page.ActivateText, page.CanActivate));
        _f.Server.WorkflowStateGate.SetResult();
        await first;
        _ = Assert.Single(_f.Server.ActivatedWorkflows);
    }

    [Fact]
    public async Task A_refusal_is_said_in_words_and_the_button_stays()
    {
        var page = await Opened(Off);
        _f.Server.ActivateWorkflowAnswer = RouteAnswer.Problem("workflows-slug-activate", "not-found");

        await page.Activate(Token);

        Assert.StartsWith("Couldn't activate the workflow. ", page.StateNoticeText, StringComparison.Ordinal);
        Assert.Equal((true, true, false), (page.HasStateNotice, page.CanActivate, page.IsChangingState));
    }

    [Fact]
    public async Task No_answer_from_Sindri_says_the_change_may_have_applied()
    {
        var page = await Opened(Slug);
        _f.Server.GoDown();

        await page.Pause(Token);

        Assert.StartsWith("Sindri didn't answer, so the change may have applied", page.StateNoticeText, StringComparison.Ordinal);
        Assert.False(page.IsChangingState);
    }

    [Fact]
    public async Task The_note_goes_when_another_workflow_opens()
    {
        var page = await Opened(Off);
        _f.Server.ActivateWorkflowAnswer = RouteAnswer.Problem("workflows-slug-activate", "not-found");
        await page.Activate(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.False(page.HasStateNotice);
    }
}
