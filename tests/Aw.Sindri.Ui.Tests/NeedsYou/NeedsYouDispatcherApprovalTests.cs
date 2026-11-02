using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A Home row's Approve recipe and Approve profile (gh-1038): each sends its approval once, to the route the inbox bar uses, and says what came back; approve-all stays the dialog's.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouDispatcherApprovalTests
{
    private const string StepHash = "sha256:4ca37bf46333dc66a14c042bb4561aad96eed677f35f5fc221f0497c8b3e31f7";   // workflow.step v5 in the served recipe list (the workflow-recipes list golden)
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private NeedsYouDispatcher Dispatcher() =>
        new(_server, () => _api, new InstanceCommander(_server, () => _api), new DeskReporter(_server, () => _api));

    [Fact]
    public async Task A_home_recipe_row_posts_one_approval_for_the_version_the_list_serves()
    {
        var row = NeedsYouRows.Recipe();

        var result = await Dispatcher().Run(row, row.Actions[0]);

        var sent = Assert.Single(_server.RecipeApprovals);
        Assert.Equal(("workflow.step", StepHash, DispatchOutcome.Sent), (sent.Id, sent.Hash, result.Outcome));
    }

    [Fact]
    public async Task A_home_recipe_row_that_names_its_hash_approves_that_hash()
    {
        var row = NeedsYouRows.Recipe() with { Cli = "sindri recipe approve workflow.step@" + StepHash };

        _ = await Dispatcher().Run(row, row.Actions[0]);

        Assert.Equal(StepHash, Assert.Single(_server.RecipeApprovals).Hash);
    }

    [Fact]
    public async Task A_refused_recipe_approval_is_said_with_its_code_and_the_rows_command()
    {
        _server.ApproveRecipeAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-WF-009", "That version changed again.");
        var row = NeedsYouRows.Recipe();

        var result = await Dispatcher().Run(row, row.Actions[0]);

        Assert.Equal(DispatchOutcome.Refused, result.Outcome);
        Assert.Contains("SND-WF-009", result.Message, StringComparison.Ordinal);
        Assert.Contains(row.Cli, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_home_profile_row_posts_the_full_hash_once()
    {
        var row = NeedsYouRows.ProfileReady();

        var result = await Dispatcher().Run(row, row.Actions[0]);

        Assert.Equal(NeedsYouRows.ProfileHash, Assert.Single(_server.ProfileApprovals).Hash);
        Assert.Equal(DispatchOutcome.Sent, result.Outcome);
    }

    [Fact]
    public async Task A_profile_approval_that_did_not_go_through_is_refused_in_words()
    {
        _server.ApproveProfileAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-PROFILE-009", "The settings changed again.");
        var row = NeedsYouRows.ProfileReady();

        var result = await Dispatcher().Run(row, row.Actions[0]);

        Assert.Equal(DispatchOutcome.Refused, result.Outcome);
        Assert.Contains("SND-PROFILE-009", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_action_with_no_send_is_still_said_on_the_row()
    {
        var row = NeedsYouRows.Park();
        var other = new NeedsYouAction("GrantRounds", "Grant rounds", ActionReadiness.Ready, "sindri workflow answer x --grant 1", RejectLike: false);

        var result = await Dispatcher().Run(row, other);

        Assert.Equal((DispatchOutcome.Unhandled, Labels.RowNotAvailable("sindri workflow answer x --grant 1")), (result.Outcome, result.Message));
    }
}
