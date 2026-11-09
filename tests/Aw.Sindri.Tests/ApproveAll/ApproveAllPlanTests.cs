using Aw.Kernel.Testing;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.GetApproveAllPlan;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using static Aw.Sindri.Tests.ApproveAll.ApproveAllFixtures;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>The approve-all plan (setup-once spec §5.3, §7.2): refused before a profile is approved, then the engine's rows with the views' freshness.</summary>
[Lane(TestLane.Unit)]
public sealed class ApproveAllPlanTests
{
    private readonly SliceHarness _slice = new();
    private readonly ProjectorProgress _progress;

    public ApproveAllPlanTests()
    {
        _progress = new ProjectorProgress(_slice.Clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetApproveAllPlanService Service(FakeShippedRecipes shipped) => new(new ApproveAllPlanReader(_slice.Profiles, shipped), _progress);

    private async Task Approved() => _ = await _slice.Profiles.Save(SliceHarness.Profile(), Token);

    [Fact]
    public async Task With_no_profile_the_plan_is_refused()
    {
        var result = await Service(new FakeShippedRecipes(StepAndItem())).Handle(new GetApproveAllPlanRequest(), Token);

        Assert.Equal("SND-PROFILE-001", result.Error.Code);
    }

    [Fact]
    public async Task The_plan_lists_the_rows_children_first_with_a_digest_and_no_empty_sentence()
    {
        await Approved();
        var shipped = new FakeShippedRecipes(StepAndItem());
        shipped.OwnApproval.Add(ShipNeedsItsOwn());

        var plan = (await Service(shipped).Handle(new GetApproveAllPlanRequest(), Token)).Value;

        Assert.Equal([Step, Item], plan.Recipes.Select(static r => r.Id));
        Assert.Equal([PlanChange.Updated, PlanChange.New], plan.Recipes.Select(static r => r.Change));
        Assert.Equal([Ship], plan.OwnApproval.Select(static r => r.Id));
        Assert.Equal(ApproveAllDigest.Of(plan.Recipes), plan.Digest);
        Assert.Null(plan.Empty);
    }

    [Fact]
    public async Task A_plan_with_nothing_waiting_says_every_recipe_is_approved()
    {
        await Approved();

        var plan = (await Service(new FakeShippedRecipes([])).Handle(new GetApproveAllPlanRequest(), Token)).Value;

        Assert.Equal(("Every recipe is approved.", "000000"), (plan.Empty, plan.Digest));
    }

    [Fact]
    public async Task A_plan_that_only_has_a_recipe_needing_its_own_approval_is_not_empty()
    {
        await Approved();
        var shipped = new FakeShippedRecipes([]);
        shipped.OwnApproval.Add(ShipNeedsItsOwn());

        var plan = (await Service(shipped).Handle(new GetApproveAllPlanRequest(), Token)).Value;

        Assert.Null(plan.Empty);
    }

    [Fact]
    public async Task The_plan_carries_the_views_freshness()
    {
        await Approved();

        var plan = (await Service(new FakeShippedRecipes([])).Handle(new GetApproveAllPlanRequest(), Token)).Value;

        Assert.Equal(_progress.Current.AsOf, plan.AsOf);
    }
}
