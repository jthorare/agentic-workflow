using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using static Aw.Sindri.Tests.ApproveAll.ApproveAllFixtures;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>Issuing the 'approve-all &lt;digest6&gt;' challenge (setup-once spec §4 item 1): bound to the plan's digest, with the plan as its preview.</summary>
[Lane(TestLane.Unit)]
public sealed class IssueApproveAllChallengeTests
{
    private readonly SliceHarness _slice = new();
    private readonly FakeShippedRecipes _shipped = new(StepAndItem());

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IssueChallengeService Service() =>
        new(null!, null!, _slice.Challenges, null!, null!, new ApproveAllPlanReader(_slice.Profiles, _shipped));   // why: this form reads only the plan and the challenges

    private async Task Approved() => _ = await _slice.Profiles.Save(SliceHarness.Profile(), Token);

    private async Task<string> Digest() => (await _shipped.Plan(Token)).Value.Digest;

    [Fact]
    public async Task The_plans_digest_issues_a_challenge_with_the_plan_as_its_preview()
    {
        await Approved();
        var digest = await Digest();

        var issued = (await Service().Handle(new IssueChallengeRequest("approve-all " + digest), Token)).Value;

        Assert.Equal("approve-all " + digest, issued.Action);
        Assert.Equal([Step, Item], issued.ApproveAll!.Recipes.Select(static r => r.Id));
        Assert.Null(issued.Approve);
        Assert.True(_slice.Challenges.Redeem(new ChallengeAnswer(issued.Challenge, issued.Action), ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest),
            Core.Workflow.WorkflowErrors.RecipeChallengeStale).IsSuccess);
    }

    [Fact]
    public async Task Before_a_profile_is_approved_there_is_no_challenge()
    {
        var issued = await Service().Handle(new IssueChallengeRequest("approve-all " + await Digest()), Token);

        Assert.Equal("SND-SETUP-007", issued.Error.Code);
    }

    [Fact]
    public async Task A_digest_that_is_not_the_plans_is_the_recipe_challenge_refusal()
    {
        await Approved();

        var issued = await Service().Handle(new IssueChallengeRequest("approve-all 000000"), Token);

        Assert.Equal("SND-RECIPE-009", issued.Error.Code);
    }

    [Fact]
    public async Task A_plan_with_only_a_recipe_needing_its_own_approval_has_no_challenge()
    {
        await Approved();
        var shipped = new FakeShippedRecipes([]);
        shipped.OwnApproval.Add(ShipNeedsItsOwn());
        var service = new IssueChallengeService(null!, null!, _slice.Challenges, null!, null!, new ApproveAllPlanReader(_slice.Profiles, shipped));

        var issued = await service.Handle(new IssueChallengeRequest("approve-all 000000"), Token);

        Assert.Equal("SND-CLI-003", issued.Error.Code);
    }

    [Fact]
    public async Task A_plan_with_nothing_in_it_has_no_challenge()
    {
        await Approved();
        var empty = new IssueChallengeService(null!, null!, _slice.Challenges, null!, null!, new ApproveAllPlanReader(_slice.Profiles, new FakeShippedRecipes([])));

        var issued = await empty.Handle(new IssueChallengeRequest("approve-all 000000"), Token);

        Assert.Equal("SND-CLI-003", issued.Error.Code);
    }
}
