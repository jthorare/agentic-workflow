using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Controls;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The inbox's typed and held confirmations (needs-you spec §8.3, slice 3): what the bar shows for a drifted profile, approve-all and a changed workflow, and that only the control approves.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouConfirmBarTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly AppConfirmFixture _fixture = new();
    private readonly FakeSindriApi _server = new();
    private int _focusRequests;
    private CatalogViewModel _catalog = null!;   // the catalog the last bar was made over

    private async Task<NeedsYouBarViewModel> Bar(NeedsYouEntry entry, string ui = "ready")
    {
        _catalog = _fixture.Catalog(ui);
        var confirm = new NeedsYouConfirm(_catalog, () => _api, _server, _fixture.Clock);
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), _fixture.Clipboard, _fixture.Clock, confirm);
        bar.ConfirmFocusRequested += (_, _) => _ = ++_focusRequests;
        bar.Show(new NeedsYouRowViewModel(entry, seen: true, reducedMotion: false, arrived: false, _fixture.Clock));
        await bar.Loaded;
        bar.ReadToEnd = true;
        return bar;
    }

    private void ServeProfileChallenge() => _server.AppChallengeAnswer = RouteAnswer.Api("workflow-challenges-app", "profile");

    [Fact]
    public async Task A_drifted_profile_shows_the_typed_box_with_the_servers_challenge_text_verbatim()
    {
        ServeProfileChallenge();

        var bar = await Bar(NeedsYouRows.ProfileReady());

        var asked = Assert.Single(_server.Challenges);
        Assert.Equal("approve profile 932d87", asked.Action);   // the row's full hash, shortened as the server's challenge is
        Assert.Equal((true, false, false, "approve profile 70e158"), (bar.ShowsTyped, bar.ShowsHold, bar.ShowsTerminal, bar.TypedBox!.Phrase));   // the served text, not the one the app asked with
    }

    [Fact]
    public async Task Typing_the_phrase_and_pressing_sends_the_full_hash_with_the_answer_once_and_approves_the_row()
    {
        ServeProfileChallenge();
        var bar = await Bar(NeedsYouRows.ProfileReady());
        bar.TypedBox!.Text = "approve profile 70e158";

        await bar.SubmitTyped(bar.TypedBox.Submit()!);
        await bar.SubmitTyped("approve profile 70e158");   // a second press while approved sends nothing

        var sent = Assert.Single(_server.ProfileApprovals);
        Assert.Equal((NeedsYouRows.ProfileHash, "CHL-00000000000000000000000000000006", "approve profile 70e158"), (sent.Hash, sent.Answer.Challenge, sent.Answer.Typed));
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task Text_that_is_not_the_phrase_cannot_be_submitted()
    {
        ServeProfileChallenge();
        var bar = await Bar(NeedsYouRows.ProfileReady());
        bar.TypedBox!.Text = "approve profile 000000";

        Assert.Null(bar.TypedBox.Submit());
        Assert.Empty(_server.ProfileApprovals);
    }

    [Fact]
    public async Task A_on_a_typed_row_only_moves_to_the_box()
    {
        ServeProfileChallenge();
        var bar = await Bar(NeedsYouRows.ProfileReady());

        await bar.PressApprove();

        Assert.Equal((1, NeedsYouBarMode.Pending, false), (_focusRequests, bar.Mode, bar.IsApproved));
        Assert.Empty(_server.ProfileApprovals);
    }

    [Fact]
    public async Task A_refused_approval_shows_the_words_and_leaves_the_row_open()
    {
        ServeProfileChallenge();
        _server.ApproveProfileAnswer = RouteAnswer.Problem("profile-approve", "refused-session");
        var bar = await Bar(NeedsYouRows.ProfileReady());
        bar.TypedBox!.Text = "approve profile 70e158";

        await bar.SubmitTyped(bar.TypedBox.Submit()!);

        Assert.Equal((false, true, Labels.ApproveSessionRefused), (bar.IsApproved, bar.HasRefusal, bar.Refusal));
        _ = Assert.Single(_server.Challenges);   // an agent session is refused every time: asking again helps nobody
    }

    [Fact]
    public async Task Approve_all_shows_the_hold_and_the_hold_sends_once_and_approves_the_row()
    {
        var bar = await Bar(NeedsYouRows.SetupRecipes());

        Assert.Equal((true, false, false), (bar.ShowsHold, bar.ShowsTyped, bar.ShowsTerminal));
        Assert.StartsWith("approve-all ", Assert.Single(_server.Challenges).Action, StringComparison.Ordinal);

        await bar.HoldReached(1500);

        Assert.Equal(1500, Assert.Single(_server.Approvals).HeldMs);
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task A_on_the_hold_row_only_moves_to_the_button()
    {
        var bar = await Bar(NeedsYouRows.SetupRecipes());

        await bar.PressApprove();

        Assert.Equal((1, false), (_focusRequests, bar.IsApproved));
        Assert.Empty(_server.Approvals);
    }

    private const string StepHash = "sha256:d39861bd12222fcc4b74432567eabfd7b58660c4b8d714be71528b6a1353ba2a";   // workflow.step v3 in the served recipe list

    private void ServeRecipeChallenge() => _server.AppChallengeAnswer = RouteAnswer.SpecShaped("challenges-app", "recipe");

    [Fact]
    public async Task A_single_workflows_row_shows_the_terminal_copy_until_the_catalog_is_ready()
    {
        ServeRecipeChallenge();
        var bar = await Bar(NeedsYouRows.Recipe(), ui: "cli-only");

        Assert.Equal((true, true, false), (bar.HasBar, bar.ShowsTerminal, bar.ShowsConfirm));
        Assert.Equal((0, 0), (_server.Challenges.Count, _server.RecipesCalls));   // nothing is asked for while the app may not confirm

        await bar.CopyTerminal();
        Assert.Equal("sindri recipe approve workflow.step", _fixture.Clipboard.Text);

        AppConfirmFixture.Reread(_catalog, "ready");
        await bar.Loaded;

        Assert.Equal((false, true), (bar.ShowsTerminal, bar.ShowsTyped));
    }

    [Fact]
    public async Task A_ready_single_workflow_shows_the_typed_box_with_the_servers_challenge_for_the_served_version()
    {
        ServeRecipeChallenge();

        var bar = await Bar(NeedsYouRows.Recipe());

        Assert.Equal("approve workflow.step 3", Assert.Single(_server.Challenges).Action);
        Assert.Equal((true, false, false, "approve workflow.step 3"), (bar.ShowsTyped, bar.ShowsHold, bar.ShowsTerminal, bar.TypedBox!.Phrase));
        Assert.Empty(_server.RecipeApprovals);   // showing the box approves nothing
    }

    [Fact]
    public async Task Typing_the_phrase_sends_the_served_hash_with_the_answer_once_and_approves_the_row()
    {
        ServeRecipeChallenge();
        var bar = await Bar(NeedsYouRows.Recipe());
        bar.TypedBox!.Text = "approve workflow.step 3";

        await bar.SubmitTyped(bar.TypedBox.Submit()!);
        await bar.SubmitTyped("approve workflow.step 3");   // a second press while approved sends nothing

        var sent = Assert.Single(_server.RecipeApprovals);
        Assert.Equal(("workflow.step", StepHash, "CHL-00000000000000000000000000000007", "approve workflow.step 3"), (sent.Id, sent.Hash, sent.Answer.Challenge, sent.Answer.Typed));
        Assert.Empty(_server.ProfileApprovals);
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task Text_that_is_not_the_workflows_phrase_sends_nothing()
    {
        ServeRecipeChallenge();
        var bar = await Bar(NeedsYouRows.Recipe());

        await bar.SubmitTyped("approve workflow.step 2");

        Assert.Empty(_server.RecipeApprovals);
        Assert.False(bar.IsApproved);
    }

    [Fact]
    public async Task A_refused_workflow_approval_shows_the_words_and_leaves_the_row_open()
    {
        ServeRecipeChallenge();
        _server.ApproveRecipeAnswer = RouteAnswer.Problem("profile-approve", "refused-session");
        var bar = await Bar(NeedsYouRows.Recipe());

        await bar.SubmitTyped("approve workflow.step 3");

        Assert.Equal((false, true, Labels.ApproveSessionRefused), (bar.IsApproved, bar.HasRefusal, bar.Refusal));
    }

    [Fact]
    public async Task A_workflow_the_list_does_not_name_gets_no_box_and_no_challenge()
    {
        ServeRecipeChallenge();
        var bar = await Bar(NeedsYouRows.Recipe("workflow.gone"));

        Assert.Equal((0, Labels.NeedsYouRecipeGone), (_server.Challenges.Count, bar.Refusal));
        Assert.Empty(_server.RecipeApprovals);
    }

    [Fact]
    public void The_version_waiting_for_approval_is_the_one_asked_for()
    {
        var approved = new Aw.Sindri.Ui.Api.Workflow.RecipeSummaryView("workflow.step", 4, "sha256:aa", "approved", Name: null);
        var waiting = new Aw.Sindri.Ui.Api.Workflow.RecipeSummaryView("workflow.step", 3, "sha256:bb", "unapproved", Name: null);
        var other = new Aw.Sindri.Ui.Api.Workflow.RecipeSummaryView("workflow.other", 9, "sha256:cc", "unapproved", Name: null);

        Assert.Same(waiting, NeedsYouConfirm.Pick([approved, waiting, other], "workflow.step"));
        Assert.Null(NeedsYouConfirm.Pick([other], "workflow.step"));
    }

    [Fact]
    public async Task Text_that_is_not_the_phrase_passed_straight_to_the_bar_sends_nothing()
    {
        ServeProfileChallenge();
        var bar = await Bar(NeedsYouRows.ProfileReady());

        await bar.SubmitTyped("approve profile 000000");

        Assert.Empty(_server.ProfileApprovals);
    }

    [Fact]
    public async Task An_answer_that_did_not_approve_is_a_refusal_not_an_approval()
    {
        ServeProfileChallenge();
        _server.ApproveProfileAnswer = RouteAnswer.Api("profile-approve", "preview");   // 200 with approved:false
        var bar = await Bar(NeedsYouRows.ProfileReady());

        await bar.SubmitTyped("approve profile 70e158");

        Assert.Equal((false, Labels.NeedsYouNotApproved, 2), (bar.IsApproved, bar.Refusal, _server.Challenges.Count));   // a fresh challenge is asked for
    }

    [Fact]
    public async Task A_row_shown_before_the_catalog_is_ready_gets_its_control_once_the_catalog_loads()
    {
        ServeProfileChallenge();
        var bar = await Bar(NeedsYouRows.ProfileReady(), ui: "cli-only");
        Assert.Equal((true, false), (bar.ShowsTerminal, bar.ShowsTyped));

        AppConfirmFixture.Reread(_catalog, "ready");
        await bar.Loaded;

        Assert.Equal((false, true), (bar.ShowsTerminal, bar.ShowsTyped));
        Assert.Equal("approve profile 932d87", Assert.Single(_server.Challenges).Action);
    }

    [Fact]
    public async Task A_challenge_that_ran_out_sends_nothing_and_a_new_one_is_asked_for()
    {
        ServeProfileChallenge();
        var bar = await Bar(NeedsYouRows.ProfileReady());
        _fixture.Clock.Advance(TimeSpan.FromMinutes(6));

        await bar.SubmitTyped("approve profile 70e158");

        Assert.Equal((0, 2, Labels.NeedsYouTypedExpired), (_server.ProfileApprovals.Count, _server.Challenges.Count, bar.Refusal));
    }

    [Fact]
    public async Task A_second_press_while_the_first_is_in_flight_sends_nothing()
    {
        ServeProfileChallenge();
        var gate = _server.ApproveProfileGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bar = await Bar(NeedsYouRows.ProfileReady());

        var first = bar.SubmitTyped("approve profile 70e158");
        await bar.SubmitTyped("approve profile 70e158");
        gate.SetResult();
        await first;

        _ = Assert.Single(_server.ProfileApprovals);
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task A_not_ready_approve_all_row_stays_the_terminal_copy_and_reads_no_plan()
    {
        var bar = await Bar(NeedsYouRows.SetupRecipes(), ui: "cli-only");

        Assert.Equal((true, false, 0, 0), (bar.ShowsTerminal, bar.ShowsHold, _server.Challenges.Count, _server.PlanCalls));
    }

    [Fact]
    public async Task A_hold_the_server_refuses_shows_the_refusal_and_leaves_the_row_open()
    {
        _server.ApproveAllStartAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.Forbidden, "SND-SETUP-003", "Hold the button for the full time.");
        var bar = await Bar(NeedsYouRows.SetupRecipes());

        await bar.HoldReached(1500);

        Assert.Equal((false, true), (bar.IsApproved, bar.HasRefusal));
        _ = Assert.Single(_server.Approvals);
    }
}
