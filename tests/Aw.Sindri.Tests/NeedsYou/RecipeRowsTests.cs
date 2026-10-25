using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApproveAll;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The rows of the approve-all plan (setup-once spec §5.3, §7.3): 'setup:recipes' for the hold and 'recipe:&lt;id&gt;' for a workflow approved on its own.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeRowsTests
{
    private static readonly DateTimeOffset _t = KnownInstants.Epoch;
    private readonly PassThroughScrubber _plain = new();

    private static async Task<ShippedRecipePlan> Plan(bool own)
    {
        var shipped = new FakeShippedRecipes(ApproveAllFixtures.StepAndItem());
        if (own)
        {
            shipped.OwnApproval.Add(ApproveAllFixtures.ShipNeedsItsOwn());
        }

        return (await shipped.Plan(TestContext.Current.CancellationToken)).Value;
    }

    [Fact]
    public async Task The_hold_is_one_setup_row_counting_its_workflows()
    {
        var row = Assert.Single(NeedsYouRows.ForRecipes(await Plan(own: false), _t, _plain).Value);

        Expect.Equal(("setup:recipes", NeedsYouKind.Setup, "Approve all new workflows (2)", "2 workflows wait for your approval."), (row.Id.Value, row.Kind, row.Title, row.Reason));
        Expect.Equal((SubjectKind.Setup, "recipes", Urgency.Pulse, _t), (row.Subject.Kind, row.Subject.Id.Value, row.Urgency, row.Since));
        Expect.Equal(("ApproveAllRecipes", "sindri recipe approve-all"), (row.PrimaryAction?.Value, row.Cli));
        Expect.Sequence(["ApproveAllRecipes"], [.. row.Actions.Select(static a => a.Value)]);
    }

    [Fact]
    public async Task A_workflow_that_needs_its_own_approval_is_a_recipe_row_beside_the_hold()
    {
        var rows = NeedsYouRows.ForRecipes(await Plan(own: true), _t, _plain).Value;

        Expect.Sequence(["setup:recipes", "recipe:workflow.ship"], [.. rows.Select(static r => r.Id.Value)]);
        var row = rows[1];
        Expect.Equal((NeedsYouKind.Recipe, "Approve a changed workflow: Ship a change", SubjectKind.Recipe, "workflow.ship"), (row.Kind, row.Title, row.Subject.Kind, row.Subject.Id.Value));
        Expect.Equal(("ApproveRecipe", "sindri recipe approve workflow.ship"), (row.PrimaryAction?.Value, row.Cli));
        Expect.Equal(ApproveAllFixtures.ShipNeedsItsOwn().Sentence, row.Reason);
    }

    [Fact]
    public void A_workflow_row_carries_the_waiting_versions_id_and_hash_in_its_cli_line()
    {
        var hash = ApproveAllFixtures.Hash("5d4c3b2a1f0e");
        var own = ApproveAllFixtures.ShipNeedsItsOwn() with { Hash = hash, Version = 2 };

        var row = Assert.Single(NeedsYouRows.ForRecipes(new ShippedRecipePlan([], [own]), _t, _plain).Value);

        Assert.Equal($"sindri recipe approve workflow.ship@{hash.Value}", row.Cli);
    }

    [Fact]
    public void A_plan_with_nothing_waiting_has_no_row() => Assert.Empty(NeedsYouRows.ForRecipes(new ShippedRecipePlan([], []), _t, _plain).Value);

    [Fact]
    public void The_recipe_kind_and_subject_kind_are_written_as_recipe()
    {
        Assert.Equal("\"recipe\"", JsonSerializer.Serialize(NeedsYouKind.Recipe));
        Assert.Equal("\"recipe\"", JsonSerializer.Serialize(SubjectKind.Recipe));
    }
}
