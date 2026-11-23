using Aw.Sindri.Core.Graph.Blocks;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The contract every <see cref="ISkillCatalogPort"/> honours (issue 3787, piece 4): every skill by name, sorted, and a skill with no description listed with an empty one, never skipped.</summary>
public abstract class SkillCatalogPortContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The port under test, over these skills.</summary>
    /// <param name="skills">The skills to seed.</param>
    /// <returns>The port.</returns>
    protected abstract ValueTask<ISkillCatalogPort> Create(IReadOnlyList<SkillCatalogSeed> skills);

    [Fact]
    public async Task Every_skill_is_listed_by_name_in_name_order_with_its_description()
    {
        var port = await Create([new SkillCatalogSeed("review", "Use when someone wants a pull request looked over."), new SkillCatalogSeed("bugHunt", "Fix-and-verify loop.")]);

        var listed = await port.List(Token);

        Expect.Sequence([new SkillListing("bugHunt", "Fix-and-verify loop."), new SkillListing("review", "Use when someone wants a pull request looked over.")], listed.Value);
    }

    [Fact]
    public async Task A_skill_with_no_description_is_listed_with_an_empty_one()
    {
        var port = await Create([new SkillCatalogSeed("judge", Description: null)]);

        var listed = await port.List(Token);

        Expect.Sequence([new SkillListing("judge", string.Empty)], listed.Value);
    }

    [Fact]
    public async Task No_skills_is_an_empty_list()
    {
        var port = await Create([]);

        var listed = await port.List(Token);

        Expect.Equal(0, listed.Value.Count);
    }
}
