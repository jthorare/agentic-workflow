using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.ApproveRecipe;

namespace Aw.Sindri.Tests.ListRecipes;

/// <summary>
/// 'recipe list [--stale]' (engine spec §4.1, §6.3; README X34): one line per registered recipe with where it stands,
/// how it was approved, why it is stale, and its inputs; '--stale' keeps the approved recipes whose pins drifted from
/// the build or whose pinned child was revoked or superseded.
/// </summary>
public sealed class ListRecipesTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_registry_lists_nothing()
    {
        await using var r = new RegistryHarness();

        Assert.Empty(await r.List());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_recipe_is_listed_with_its_standing_and_inputs_by_id_then_version()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var item = await r.Shipped("sindri/workflow.item.yaml");
        _ = await r.Approve(step);

        var listed = await r.List();

        Assert.Equal([item.Hash, step.Hash], listed.Select(s => s.Hash));
        Assert.Equal(["draft", "approved"], listed.Select(s => s.Status), StringComparer.Ordinal);
        var (itemLine, stepLine) = (listed[0], listed[1]);
        Assert.Equal(RecipeStanding.Unapproved, itemLine.Standing);
        Assert.Null(itemLine.Via);
        Assert.Equal((RecipeStanding.Approved, (ApprovalVia?)ApprovalVia.Desk), (stepLine.Standing, stepLine.Via));
        Assert.Empty(stepLine.Stale);
        Assert.Equal(["fact:item"], itemLine.Inputs.Select(i => i.Key.Value), StringComparer.Ordinal);
        Assert.Equal(["fact:spec"], stepLine.Inputs.Select(i => i.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_approval_whose_pinned_blocks_drifted_is_stale_and_the_stale_filter_keeps_only_it()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Shipped("sindri/workflow.item.yaml");
        _ = await r.Approvals.Approve(RegistryData.Approval(step, new RecipePins([new BlockPin(BlockId.From("step.produce"), RegistryData.Descriptor("old"))], [])), Token);

        var line = await r.Line(step);
        var stale = await r.List(stale: true);

        Assert.Equal(RecipeStanding.Approved, line.Standing);
        Assert.Equal([StaleReason.PinDrift], line.Stale);
        Assert.Equal(("changed", "a pinned block changed"), (line.Status, line.StatusDetail));
        Assert.Equal([step.Hash], stale.Select(s => s.Hash));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Revoking_the_pinned_child_marks_its_parent_child_stale()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var item = await r.Shipped("sindri/workflow.item.yaml");
        _ = await r.Approve(step);
        _ = await r.Approve(item);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(step), Token);

        var stale = await r.List(stale: true);

        Assert.Equal(RecipeStanding.Revoked, (await r.Line(step)).Standing);
        var parent = Assert.Single(stale);
        Assert.Equal((item.Hash, RecipeStanding.Approved), (parent.Hash, parent.Standing));
        Assert.Equal([StaleReason.ChildStale], parent.Stale);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approving_a_newer_child_version_marks_its_parent_child_stale()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var item = await r.Shipped("sindri/workflow.item.yaml");
        _ = await r.Approve(step);
        _ = await r.Approve(item);
        var step2 = await r.Register(RegistryHarness.Parse(step) with { Version = 4 });
        Assert.Empty(await r.List(stale: true));

        _ = await r.Approve(step2);

        var parent = Assert.Single(await r.List(stale: true));
        Assert.Equal(item.Hash, parent.Hash);
        Assert.Equal([StaleReason.ChildStale], parent.Stale);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_approved_lists_nothing_stale()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        _ = await r.Shipped("sindri/workflow.step.yaml");

        Assert.Empty(await r.List(stale: true));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
