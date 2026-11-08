using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using static Aw.Sindri.Tests.ApproveAll.ApproveAllFixtures;

namespace Aw.Sindri.Tests.AppConfirm;

/// <summary>Issuing the app's challenge (setup-once spec §4 items 1 and 2): three action forms only, typed or held, on the app channel.</summary>
[Lane(TestLane.Unit)]
public sealed class IssueAppChallengeTests
{
    private readonly SliceHarness _slice = new();
    private readonly FakeShippedRecipes _shipped = new(StepAndItem());

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IssueAppChallengeService Service() =>
        new(new IssueChallengeService(null!, null!, _slice.Challenges, null!, null!, new ApproveAllPlanReader(_slice.Profiles, _shipped)));   // why: these forms read only the plan and the challenges

    private async Task<string> Digest() => (await _shipped.Plan(Token)).Value.Digest;

    private Kernel.Result<IssuedChallenge> Redeem(ChallengeAnswer answer, ChallengeAction action, ChallengeSubject subject) =>
        _slice.Challenges.Redeem(answer, action, subject, WorkflowErrors.RecipeChallengeStale);

    [Fact]
    public async Task Approve_all_is_confirmed_by_a_hold_or_its_text()
    {
        _ = await _slice.Profiles.Save(SliceHarness.Profile(), Token);
        var digest = await Digest();
        var action = "approve-all " + digest;

        var issued = (await Service().Handle(new IssueAppChallengeRequest(action), Token)).Value;

        Assert.Equal((action, "held", 1500, action), (issued.Action, issued.Confirm, issued.MinHoldMs, issued.TypedAlternative));
        Assert.Equal([Step, Item], issued.ApproveAll!.Recipes.Select(static r => r.Id));
        _slice.Clock.Advance(IssuedChallenge.EarliestAnswer);
        var redeemed = Redeem(new ChallengeAnswer(issued.Challenge, HeldMs: 1500), ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest));
        Assert.Equal((ChallengeMode.Held, ChallengeChannel.App), (redeemed.Value.Mode, redeemed.Value.Channel));
    }

    [Fact]
    public async Task Approve_all_before_there_is_a_profile_is_refused()
    {
        var issued = await Service().Handle(new IssueAppChallengeRequest("approve-all " + await Digest()), Token);

        Assert.Equal("SND-PROFILE-001", issued.Error.Code);
    }

    [Theory]
    [InlineData("revoke workflow.step@9ab2d40c1e77")]
    [InlineData("grant 1 T-1/step/1")]
    [InlineData("grant-tokens 100 T-1/step/1")]
    [InlineData("accept T-1/step/1")]
    [InlineData("approve workflow.step@9ab2d40c1e77")]
    [InlineData("approve-all")]
    [InlineData("approve profile")]   // the profile form went with the approval
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
        _ = await _slice.Profiles.Save(SliceHarness.Profile(), Token);
        var digest = await Digest();
        var action = "approve-all " + digest;
        var terminal = _slice.Challenges.Issue(ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest));
        var first = (await Service().Handle(new IssueAppChallengeRequest(action), Token)).Value;
        var second = (await Service().Handle(new IssueAppChallengeRequest(action), Token)).Value;
        _slice.Clock.Advance(IssuedChallenge.EarliestAnswer);

        Assert.True(Redeem(new ChallengeAnswer(first.Challenge, action), ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest)).IsError);
        Assert.True(Redeem(new ChallengeAnswer(second.Challenge, action), ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest)).IsSuccess);
        Assert.True(Redeem(new ChallengeAnswer(terminal.Id, action), ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest)).IsSuccess);
    }
}
