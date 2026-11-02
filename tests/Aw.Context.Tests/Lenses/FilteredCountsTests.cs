using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Lenses;

/// <summary>Answers say how many demoted files they carry, per role the person named, and say nothing when none (issue 1105).</summary>
public sealed class FilteredCountsTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Demoting() =>
        new(Settings: null,
            [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes")
            {
                DemotePaths = ["src/a.ts", "src/c.ts", "src/d.ts"],
                Roles = [new RepoRole("fixture", Demoted: true, ["src/c.ts"]), new RepoRole("test", Demoted: true, ["src/a.ts", "src/d.ts"])],
            }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    private async Task Import() =>
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"code:src/a.ts\", label: \"a.ts\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "  - {key: \"code:src/c.ts\", label: \"c.ts\"}",
            "  - {key: \"code:src/d.ts\", label: \"d.ts\"}",
            "edges:",
            "  - {from: \"code:src/a.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"code:src/c.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"code:src/d.ts\", to: \"feature:kept\", type: implements}"));

    [Fact, Trait("Lane", "unit")]
    public async Task Lenses_count_the_demoted_files_per_role()
    {
        await Import();

        var view = (await _world.Engine.Lenses("feature:kept", read: MapRead.Current, depth: null, Demoting(), MapWorld.None)).Value;

        // why: the demoted files are counted, so Filtered is set
        Assert.Equal(2, view.Filtered!.Demoted["test"]);
        Assert.Equal(1, view.Filtered.Demoted["fixture"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lenses_set_counts_the_demoted_files_per_role()
    {
        await Import();

        var view = (await _world.Engine.LensesOf(["feature:kept"], MapRead.Current, new LensRange(Depth: null, Hops: null), Demoting(), MapWorld.None)).Value;

        // why: the demoted files are counted, so Filtered is set
        Assert.Equal(2, view.Filtered!.Demoted["test"]);
        Assert.Equal(1, view.Filtered.Demoted["fixture"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_query_counts_the_demoted_files_it_carries_per_role()
    {
        await Import();

        var result = (await _world.Engine.Query(new ContextQuery(Text: "Kept"), Demoting(), MapWorld.None)).Value;

        // why: the demoted files are counted, so Filtered is set
        Assert.Equal(2, result.Filtered!.Demoted["test"]);
        Assert.Equal(1, result.Filtered.Demoted["fixture"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_demoted_leaves_the_block_out()
    {
        await Import();

        var lenses = (await _world.Engine.Lenses("feature:kept", read: MapRead.Current, depth: null, MapWorld.Human, MapWorld.None)).Value;
        var result = (await _world.Engine.Query(new ContextQuery(Text: "Kept"), MapWorld.Human, MapWorld.None)).Value;

        Assert.Null(lenses.Filtered);
        Assert.Null(result.Filtered);
    }
}
