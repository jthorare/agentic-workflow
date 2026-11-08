using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.IssueChallenge;

namespace Aw.Sindri.Tests.AppConfirm;

/// <summary>Issuing the app's challenge (setup-once spec §4 items 1 and 2): the one action form left (approve profile), typed, on the app channel.</summary>
[Lane(TestLane.Unit)]
public sealed class IssueAppChallengeTests
{
    private readonly SliceHarness _slice = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IssueAppChallengeService Service() =>
        new(new IssueChallengeService(_slice.Challenges, _slice.Profiles));

    private Kernel.Result<IssuedChallenge> Redeem(ChallengeAnswer answer, ChallengeAction action, ChallengeSubject subject) =>
        _slice.Challenges.Redeem(answer, action, subject, WorkflowErrors.RecipeChallengeStale);

    [Fact]
    public async Task The_profile_is_confirmed_by_typing()
    {
        var live = SliceHarness.Profile();
        _slice.Profiles.Live = live;
        var action = "approve profile " + live.Hash.Abbreviated;

        var issued = (await Service().Handle(new IssueAppChallengeRequest(action), Token)).Value;

        Assert.Equal((action, "typed", null, null), (issued.Action, issued.Confirm, issued.MinHoldMs, issued.TypedAlternative));
        _slice.Clock.Advance(IssuedChallenge.EarliestAnswer);
        var redeemed = Redeem(new ChallengeAnswer(issued.Challenge, action), ChallengeAction.ApproveProfile(live.Hash), ChallengeSubject.ForProfile(live.Hash));
        Assert.Equal((ChallengeMode.Typed, ChallengeChannel.App), (redeemed.Value.Mode, redeemed.Value.Channel));
    }

    [Theory]
    [InlineData("revoke workflow.step@9ab2d40c1e77")]
    [InlineData("grant 1 T-1/step/1")]
    [InlineData("grant-tokens 100 T-1/step/1")]
    [InlineData("accept T-1/step/1")]
    [InlineData("approve workflow.step@9ab2d40c1e77")]
    [InlineData("approve-all")]
    [InlineData("approve-all 0a1b2c")]
    [InlineData("approve workflow.step 3")]
    [InlineData("approve profile")]
    [InlineData("stop")]
    [InlineData("")]
    public async Task Any_other_action_is_refused_as_a_human_decision(string action)
    {
        var issued = await Service().Handle(new IssueAppChallengeRequest(action), Token);

        Assert.Equal("SND-AUTH-002", issued.Error.Code);
    }

    [Fact]
    public async Task A_second_app_challenge_replaces_the_first_but_not_a_terminal_one()
    {
        var live = SliceHarness.Profile();
        _slice.Profiles.Live = live;
        var action = "approve profile " + live.Hash.Abbreviated;
        var terminal = _slice.Challenges.Issue(ChallengeAction.ApproveProfile(live.Hash), ChallengeSubject.ForProfile(live.Hash));
        var first = (await Service().Handle(new IssueAppChallengeRequest(action), Token)).Value;
        var second = (await Service().Handle(new IssueAppChallengeRequest(action), Token)).Value;
        _slice.Clock.Advance(IssuedChallenge.EarliestAnswer);

        Assert.True(Redeem(new ChallengeAnswer(first.Challenge, action), ChallengeAction.ApproveProfile(live.Hash), ChallengeSubject.ForProfile(live.Hash)).IsError);
        Assert.True(Redeem(new ChallengeAnswer(second.Challenge, action), ChallengeAction.ApproveProfile(live.Hash), ChallengeSubject.ForProfile(live.Hash)).IsSuccess);
        Assert.True(Redeem(new ChallengeAnswer(terminal.Id, action), ChallengeAction.ApproveProfile(live.Hash), ChallengeSubject.ForProfile(live.Hash)).IsSuccess);
    }
}
