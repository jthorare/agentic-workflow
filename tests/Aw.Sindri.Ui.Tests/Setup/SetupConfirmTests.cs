using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The two approvals in their rows (setup spec §8.3; q93): the profile's Approve button and the workflows' plan with its Approve button, each one plain click.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupConfirmTests
{
    private const string LiveHash = "70e1581b3ecaf617318482f6b0844b67f79a287a7136b4e4b6ff5d26005e4261";

    private readonly SetupFixture _f = new("extra-setup-ready") { Hash = LiveHash };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SetupStepViewModel Step(SetupViewModel setup, string id) => setup.Steps.Single(s => s.Id == id);

    private async Task<SetupStepViewModel> ProfileRow()
    {
        _f.Serve("profile-needs-approval");
        return Step(await _f.Read(Token), "profile");
    }

    [Fact]
    public async Task The_profile_row_offers_one_approve_button_in_place_of_the_terminal_copy()
    {
        var row = await ProfileRow();

        Assert.Null(row.Action);   // the Approve button takes the lock button's place
        Assert.Equal((true, true), (row.Profile!.ShowsApprove, row.Profile.CanApprove));
        Assert.Empty(_f.Server.ProfileApprovals);   // showing it approves nothing
    }

    [Fact]
    public async Task One_click_sends_the_live_hash_once_with_no_challenge_and_approves_the_row()
    {
        var profile = (await ProfileRow()).Profile!;

        await profile.Approve();
        await profile.Approve();   // a second press after the approval sends nothing

        Assert.Equal(LiveHash, Assert.Single(_f.Server.ProfileApprovals).Hash);
        Assert.True(profile.IsDone);
    }

    [Fact]
    public async Task A_stale_hash_says_to_read_it_again_and_leaves_the_button_pressable()
    {
        _f.Server.ApproveProfileAnswer = RouteAnswer.Problem("profile-approve", "stale-hash");
        var profile = (await ProfileRow()).Profile!;

        await profile.Approve();

        Assert.Equal((Labels.SetupProfileChangedWhileReading, false, true), (profile.Refusal, profile.IsDone, profile.CanApprove));
    }

    [Fact]
    public async Task Without_a_profile_hash_the_row_says_it_is_reading_and_offers_no_button()
    {
        _f.Hash = null;

        var profile = (await ProfileRow()).Profile!;

        Assert.Equal((false, true), (profile.ShowsApprove, profile.IsWaiting));
    }

    [Fact]
    public async Task Workflows_row_shows_the_plan_with_its_change_words_and_the_approve_button()
    {
        _f.Serve(With("profile-needs-approval", "recipes", "needs-you", "ApproveAllRecipes"));

        var row = Step(await _f.Read(Token), "recipes");
        var all = row.Workflows!;

        Assert.Null(row.Action);
        Assert.Equal((true, true, true), (all.IsOpen, all.ShowsPlan, all.CanApprove));
        Assert.NotEmpty(all.Rows);
        Assert.All(all.Rows, r => Assert.NotEmpty(r.Reason));   // New, Updated or Needs a second look, as the plan serves it
        Assert.Empty(_f.Server.Approvals);   // reading the plan approves nothing
    }

    [Fact]
    public async Task A_workflows_row_that_stops_needing_the_person_closes_its_plan()
    {
        _f.Serve(With("profile-needs-approval", "recipes", "needs-you", "ApproveAllRecipes"));
        var setup = await _f.Read(Token);

        _f.Serve(With("profile-needs-approval", "recipes", "todo"));
        await setup.Refresh(Url, Token);

        Assert.False(Step(setup, "recipes").Workflows!.IsOpen);
    }

    [Fact]
    public async Task The_profile_row_reads_the_summary_once_per_hash_and_shows_it_even_before_the_app_may_approve()
    {
        _f.Server.ProfileAnswer = RouteAnswer.Api("profile", "repo-added");
        _f.Serve("profile-needs-approval");

        var setup = await _f.Read(Token);
        await setup.Refresh(Url, Token);

        var profile = Step(setup, "profile").Profile!;
        Assert.True(profile.ShowsApprove);
        Assert.Equal(["demo"], profile.Summary.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(1, _f.Server.ProfileCalls);
    }

    [Fact]
    public async Task A_profile_row_that_stops_needing_the_person_drops_its_summary()
    {
        _f.Server.ProfileAnswer = RouteAnswer.Api("profile", "repo-added");
        var setup = await _f.Read(Token);
        _f.Serve(With("profile-needs-approval", "profile", "done"));

        await setup.Refresh(Url, Token);

        Assert.False(Step(setup, "profile").Profile!.Summary.HasSummary);
    }

    [Fact]
    public async Task A_summary_that_cannot_be_read_leaves_the_row_working_and_is_asked_for_again()
    {
        _f.Server.ProfileAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-001", "The control plane is down");
        _f.Serve("profile-needs-approval");
        var setup = await _f.Read(Token);

        _f.Server.ProfileAnswer = RouteAnswer.Api("profile", "changed");
        await setup.Refresh(Url, Token);

        Assert.Equal(["Services"], Step(setup, "profile").Profile!.Summary.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(2, _f.Server.ProfileCalls);
    }

    [Fact]
    public async Task A_served_summary_takes_the_place_of_the_steps_own_preview()
    {
        _f.Server.ProfileAnswer = RouteAnswer.Api("profile", "repo-added");
        _f.Serve("profile-preview");

        var step = Step(await _f.Read(Token), "profile");

        Assert.True(step.Profile!.Summary.HasSummary);
        Assert.NotEmpty(step.Preview);
        Assert.False(step.HasPreview);
    }

    [Fact]
    public async Task Without_a_summary_the_steps_own_preview_stays()
    {
        _f.Serve("profile-preview");   // the default profile read has no summary

        var step = Step(await _f.Read(Token), "profile");

        Assert.False(step.Profile!.Summary.HasSummary);
        Assert.True(step.HasPreview);
    }
}
