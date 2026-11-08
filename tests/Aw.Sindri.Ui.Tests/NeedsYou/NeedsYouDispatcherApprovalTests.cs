using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A Home row's Approve profile (gh-1038): it sends its approval once, to the route the inbox bar uses, and says what came back.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouDispatcherApprovalTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private NeedsYouDispatcher Dispatcher() =>
        new(_server, () => _api);

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
