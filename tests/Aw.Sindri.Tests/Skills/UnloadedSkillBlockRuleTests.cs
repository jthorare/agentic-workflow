using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.Workflow.Wiring;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Skills;

/// <summary>A recipe naming a skill block that did not load fails validation with the reason the block was skipped (skill flows spec §3.2), not with the plain "isn't registered"; the rest of the catalog is untouched.</summary>
[Lane(TestLane.Unit)]
public sealed class UnloadedSkillBlockRuleTests
{
    private static BlockCatalog CatalogWith(params UnloadedBlock[] unloaded) => new(WiringCatalog.WithTestBlocks.Blocks, [], sentences: null, unloaded);

    private static Core.Workflow.Recipes.Recipe Naming(string block) =>
        Of("produce", [("produce", Active(block)), ("done", Terminal())], [Go("produce", "done", when: Outcome("done"))]);

    [Fact]
    public void A_recipe_naming_a_block_that_did_not_load_gets_the_skip_reason()
    {
        var catalog = CatalogWith(new UnloadedBlock(BlockId.From("step.skill.review"), "args name '{missing}', which no input declares"));

        var lines = CheckBlocks(Naming("step.skill.review"), new Located("r.yaml", [], catalog)).Lines();

        var line = Assert.Single(lines);
        Assert.StartsWith("RCP-BLOCK-001: ", line, StringComparison.Ordinal);
        Assert.Contains("skills:block review is not loaded: args name '{missing}', which no input declares", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_block_nobody_skipped_is_still_the_plain_not_registered()
    {
        var catalog = CatalogWith(new UnloadedBlock(BlockId.From("step.skill.review"), "a reason"));

        var line = Assert.Single(CheckBlocks(Naming("step.skill.autoplan"), new Located("r.yaml", [], catalog)).Lines());

        Assert.DoesNotContain("skills:block", line, StringComparison.Ordinal);
        Assert.Contains("isn't registered", line, StringComparison.Ordinal);
    }
}
