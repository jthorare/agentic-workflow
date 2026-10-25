using System.Text.Json.Nodes;
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
    private const string StepHash = "sha256:4ca37bf46333dc66a14c042bb4561aad96eed677f35f5fc221f0497c8b3e31f7";   // workflow.step v5 in the served recipe list (the workflow-recipes list golden)
    private const string WaitingHash = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";   // the shipped version that gained a capability
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly FakeSindriApi _server = new();

    private async Task<NeedsYouBarViewModel> Bar(NeedsYouEntry entry)
    {
        var confirm = new NeedsYouConfirm(new CatalogViewModel(new FakeClipboard(), _clock), () => _api, _server, _clock);
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), _clock, confirm);
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
        _server.ApproveProfileAnswer = RouteAnswer.Refused();
        var entry = NeedsYouRows.ProfileReady();
        var bar = await Bar(entry);

        await bar.PressApprove();

        Assert.Equal((false, true, "SND-AUTH-002: " + Labels.ApproveSessionRefused + " Run: " + entry.Cli), (bar.IsApproved, bar.HasRefusal, bar.Refusal));
        Assert.True(bar.PrimaryEnabled);
    }

    [Fact]
    public async Task Any_refused_profile_approval_names_its_code_and_the_rows_command()
    {
        _server.ApproveProfileAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.Conflict, "SND-PROFILE-009", "The settings changed again.");
        var entry = NeedsYouRows.ProfileReady();
        var bar = await Bar(entry);

        await bar.PressApprove();

        Assert.Equal((false, "SND-PROFILE-009: The settings changed again. Run: sindri profile approve " + NeedsYouRows.ProfileHash[..6]), (bar.IsApproved, bar.Refusal));
        Assert.Equal(entry.Cli, "sindri profile approve " + NeedsYouRows.ProfileHash[..6]);   // the served line, not a built one
    }

    [Fact]
    public async Task An_answer_that_did_not_approve_is_a_refusal_not_an_approval()
    {
        _server.ApproveProfileAnswer = RouteAnswer.Api("profile-approve", "preview");   // 200 with approved:false
        var bar = await Bar(NeedsYouRows.ProfileReady());

        await bar.PressApprove();

        Assert.Equal((false, Labels.NeedsYouNotApproved + " Run: " + NeedsYouRows.ProfileReady().Cli), (bar.IsApproved, bar.Refusal));
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
        _server.ApproveRecipeAnswer = RouteAnswer.Refused();
        var entry = NeedsYouRows.Recipe();
        var bar = await Bar(entry);

        await bar.PressApprove();

        Assert.Equal((false, true, "SND-AUTH-002: " + Labels.ApproveSessionRefused + " Run: " + entry.Cli), (bar.IsApproved, bar.HasRefusal, bar.Refusal));
    }

    [Fact]
    public async Task A_workflow_the_list_does_not_name_is_not_approved()
    {
        var entry = NeedsYouRows.Recipe("workflow.gone");
        var bar = await Bar(entry);

        await bar.PressApprove();

        Assert.Equal("SND-UI-004: " + Labels.NeedsYouRecipeGone + " Run: " + entry.Cli, bar.Refusal);   // the code and the served command
        Assert.Equal("sindri recipe approve workflow.gone", entry.Cli);
        Assert.Empty(_server.RecipeApprovals);
    }

    // The served list as #983 has it: the approved row, and the shipped version that gained a capability as a row of its own.
    private void ServeListWithWaitingVersion()
    {
        var list = JsonNode.Parse(GoldenFiles.Api("workflow-recipes", "list"))!;   // why: the golden is a JSON document
        var rows = list["recipes"]!.AsArray();
        var step = rows.Single(r => (string?)r!["id"] == "workflow.step")!;
        var waiting = JsonNode.Parse(step.ToJsonString())!;
        waiting["version"] = 5;
        waiting["hash"] = WaitingHash;
        waiting["standing"] = "unapproved";
        waiting["status"] = "changed";
        waiting["gainsCapability"] = true;
        rows.Add(waiting);
        _server.RecipeListAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
    }

    [Fact]
    public async Task A_workflow_that_gained_a_capability_is_approved_at_the_waiting_hash()
    {
        ServeListWithWaitingVersion();
        var bar = await Bar(NeedsYouRows.Recipe() with { Cli = "sindri recipe approve workflow.step" });

        await bar.PressApprove();

        var sent = Assert.Single(_server.RecipeApprovals);
        Assert.Equal(("workflow.step", WaitingHash), (sent.Id, sent.Hash));
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task The_hash_the_rows_command_names_is_the_one_approved()
    {
        ServeListWithWaitingVersion();
        var bar = await Bar(NeedsYouRows.Recipe() with { Cli = "sindri recipe approve workflow.step@" + StepHash });

        await bar.PressApprove();

        Assert.Equal(StepHash, Assert.Single(_server.RecipeApprovals).Hash);
    }

    [Fact]
    public async Task The_waiting_hash_the_rows_command_names_is_approved()
    {
        ServeListWithWaitingVersion();
        var bar = await Bar(NeedsYouRows.Recipe() with { Cli = "sindri recipe approve workflow.step@" + WaitingHash });

        await bar.PressApprove();

        Assert.Equal(WaitingHash, Assert.Single(_server.RecipeApprovals).Hash);
    }

    [Fact]
    public void A_version_that_gains_a_capability_wins_over_a_newer_approved_one_and_a_named_hash_wins_over_both()
    {
        var old = new Ui.Api.Workflow.RecipeSummaryView("workflow.step", 4, "sha256:aa", "approved", Name: null);
        var gains = new Ui.Api.Workflow.RecipeSummaryView("workflow.step", 3, "sha256:bb", "approved", Name: null, GainsCapability: true);

        Assert.Same(gains, NeedsYouConfirm.Pick([old, gains], "workflow.step"));
        Assert.Same(old, NeedsYouConfirm.Pick([old, gains], "workflow.step", "sha256:aa"));
        Assert.Equal("sha256:bb", NeedsYouConfirm.HashOf("sindri recipe approve workflow.step@sha256:bb"));
        Assert.Equal("", NeedsYouConfirm.HashOf("sindri recipe approve workflow.step"));
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
