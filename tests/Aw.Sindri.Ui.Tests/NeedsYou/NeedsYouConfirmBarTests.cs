using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The inbox's approvals (needs-you spec §8.3; q93): what the bar offers for a drifted profile, approve-all and a changed workflow, and that one plain click approves with no challenge.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouConfirmBarTests
{
    private const string StepHash = "sha256:f039848fd714d2a46a4d3d216083bff1815821cd3e3b4f1bc6402bbb44bd54fa";   // workflow.step v3 in the served recipe list
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly FakeSindriApi _server = new();

    private async Task<NeedsYouBarViewModel> Bar(NeedsYouEntry entry)
    {
        var confirm = new NeedsYouConfirm(new CatalogViewModel(new FakeClipboard(), _clock), () => _api, _server, _clock);
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), confirm);
        bar.Show(new NeedsYouRowViewModel(entry, seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        bar.ReadToEnd = true;
        return bar;
    }

    [Fact]
    public async Task A_drifted_profile_offers_the_approve_button_and_approves_nothing_by_showing_it()
    {
        var bar = await Bar(NeedsYouRows.ProfileReady());

        Assert.Equal((true, true, NeedsYouBarMode.Pending), (bar.ShowsPrimary, bar.PrimaryEnabled, bar.Mode));
        Assert.Empty(_server.ProfileApprovals);
    }

    [Fact]
    public async Task One_press_sends_the_full_hash_once_and_approves_the_row()
    {
        var bar = await Bar(NeedsYouRows.ProfileReady());

        await bar.PressApprove();
        await bar.PressApprove();   // a second press while approved sends nothing

        Assert.Equal(NeedsYouRows.ProfileHash, Assert.Single(_server.ProfileApprovals).Hash);
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task A_refused_approval_shows_the_words_and_leaves_the_row_open()
    {
        _server.ApproveProfileAnswer = RouteAnswer.Problem("profile-approve", "refused-session");
        var bar = await Bar(NeedsYouRows.ProfileReady());

        await bar.PressApprove();

        Assert.Equal((false, true, Labels.ApproveSessionRefused), (bar.IsApproved, bar.HasRefusal, bar.Refusal));
        Assert.True(bar.PrimaryEnabled);
    }

    [Fact]
    public async Task An_answer_that_did_not_approve_is_a_refusal_not_an_approval()
    {
        _server.ApproveProfileAnswer = RouteAnswer.Api("profile-approve", "preview");   // 200 with approved:false
        var bar = await Bar(NeedsYouRows.ProfileReady());

        await bar.PressApprove();

        Assert.Equal((false, Labels.NeedsYouNotApproved), (bar.IsApproved, bar.Refusal));
    }

    [Fact]
    public async Task A_second_press_while_the_first_is_in_flight_sends_nothing()
    {
        var gate = _server.ApproveProfileGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bar = await Bar(NeedsYouRows.ProfileReady());

        var first = bar.PressApprove();
        await bar.PressApprove();
        gate.SetResult();
        await first;

        _ = Assert.Single(_server.ProfileApprovals);
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task Approve_all_offers_the_button_and_one_press_sends_once_and_approves_the_row()
    {
        var bar = await Bar(NeedsYouRows.SetupRecipes());

        Assert.Equal((true, true), (bar.ShowsPrimary, bar.PrimaryEnabled));
        Assert.Empty(_server.Approvals);

        await Finish(bar.PressApprove());

        _ = Assert.Single(_server.Approvals);
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task An_approve_all_the_server_refuses_shows_the_refusal_and_leaves_the_row_open()
    {
        _server.ApproveAllStartAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.Forbidden, "SND-AUTH-002", "An agent session can't approve.");
        var bar = await Bar(NeedsYouRows.SetupRecipes());

        await Finish(bar.PressApprove());

        Assert.Equal((false, true), (bar.IsApproved, bar.HasRefusal));
        _ = Assert.Single(_server.Approvals);
    }

    [Fact]
    public async Task A_single_workflow_offers_the_button_and_one_press_sends_the_served_hash_once()
    {
        var bar = await Bar(NeedsYouRows.Recipe());

        Assert.Empty(_server.RecipeApprovals);   // showing the button approves nothing
        await bar.PressApprove();
        await bar.PressApprove();

        var sent = Assert.Single(_server.RecipeApprovals);
        Assert.Equal(("workflow.step", StepHash), (sent.Id, sent.Hash));
        Assert.Empty(_server.ProfileApprovals);
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task A_refused_workflow_approval_shows_the_words_and_leaves_the_row_open()
    {
        _server.ApproveRecipeAnswer = RouteAnswer.Problem("profile-approve", "refused-session");
        var bar = await Bar(NeedsYouRows.Recipe());

        await bar.PressApprove();

        Assert.Equal((false, true, Labels.ApproveSessionRefused), (bar.IsApproved, bar.HasRefusal, bar.Refusal));
    }

    [Fact]
    public async Task A_workflow_the_list_does_not_name_is_not_approved()
    {
        var bar = await Bar(NeedsYouRows.Recipe("workflow.gone"));

        await bar.PressApprove();

        Assert.Equal(Labels.NeedsYouRecipeGone, bar.Refusal);
        Assert.Empty(_server.RecipeApprovals);
    }

    [Fact]
    public void The_version_waiting_for_approval_is_the_one_approved()
    {
        var approved = new Ui.Api.Workflow.RecipeSummaryView("workflow.step", 4, "sha256:aa", "approved", Name: null);
        var waiting = new Ui.Api.Workflow.RecipeSummaryView("workflow.step", 3, "sha256:bb", "unapproved", Name: null);
        var other = new Ui.Api.Workflow.RecipeSummaryView("workflow.other", 9, "sha256:cc", "unapproved", Name: null);

        Assert.Same(waiting, NeedsYouConfirm.Pick([approved, waiting, other], "workflow.step"));
        Assert.Null(NeedsYouConfirm.Pick([other], "workflow.step"));
    }

    // The clock does not move on its own: the approval job's polling is bounded and nudged a beat at a time.
    private async Task Finish(Task task)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(20));
        while (!task.IsCompleted)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(1, bound.Token);
        }

        await task;
    }
}
