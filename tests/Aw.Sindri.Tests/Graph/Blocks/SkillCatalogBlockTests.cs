using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>The 'skill_catalog' block (issue 3787, piece 4).</summary>
[Lane(TestLane.Unit)]
public sealed class SkillCatalogBlockTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task It_hands_on_every_skill_with_its_description_and_the_count()
    {
        var result = await new SkillCatalogBlock(new FakeSkillCatalog([new SkillCatalogSeed("review", "Looks a pull request over."), new SkillCatalogSeed("judge", Description: null)]))
            .Execute(BlockFakes.Context([]), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        var skills = result.Value.Outputs["skills"]!.AsArray().Select(static s => (s!["name"]!.GetValue<string>(), s["description"]!.GetValue<string>()));
        Assert.Equal([("judge", string.Empty), ("review", "Looks a pull request over.")], skills);
        Assert.Equal(2, result.Value.Outputs["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_long_description_is_cut_so_the_list_stays_small()
    {
        var result = await new SkillCatalogBlock(new FakeSkillCatalog([new SkillCatalogSeed("long", new string('d', 2000))])).Execute(BlockFakes.Context([]), Token);

        Assert.Equal(SkillCatalogBlock.DescriptionChars, result.Value.Outputs["skills"]![0]!["description"]!.GetValue<string>().Length);
    }

    [Fact]
    public async Task A_catalog_that_can_t_be_read_fails_the_node_with_its_reason()
    {
        var result = await new SkillCatalogBlock(new FailingCatalog()).Execute(BlockFakes.Context([]), Token);

        Assert.True(result.IsError);
        Assert.Contains("unreadable", result.Error.Message, StringComparison.Ordinal);
    }

    private sealed class FailingCatalog : ISkillCatalogPort
    {
        public ValueTask<Result<IReadOnlyList<SkillListing>>> List(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<IReadOnlyList<SkillListing>>(SindriErrors.GraphBlockFailed.With("the skills folder is unreadable")));
    }
}
