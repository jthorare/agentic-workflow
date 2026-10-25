using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Lenses;

/// <summary>A lens's code band says which of its files the repo demotes, so a consumer can tell them from product files (issue 1114).</summary>
public sealed class DemotedCodeBandFlagTests : IDisposable
{
    private const string Demoted = "src/a.ts";
    private const string Product = "src/b.ts";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Demoting(params string[] globs) =>
        new(Settings: null, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DemotePaths = [.. globs] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    private async Task<IReadOnlyList<LensFile>> CodeBand()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"code:src/a.ts\", label: \"a.ts\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "edges:",
            "  - {from: \"code:src/a.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}"));
        var lenses = (await _world.Engine.Lenses("feature:kept", read: MapRead.Current, depth: null, Demoting(Demoted), MapWorld.None)).Value;
        return lenses.Bands.Code.Files;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_demoted_file_in_the_code_band_says_it_is_demoted()
    {
        var files = await CodeBand();

        Assert.True(files.Single(f => string.Equals(f.Path, Demoted, StringComparison.Ordinal)).Demoted);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_product_file_in_the_code_band_is_not_marked_demoted()
    {
        var files = await CodeBand();

        Assert.False(files.Single(f => string.Equals(f.Path, Product, StringComparison.Ordinal)).Demoted);
    }
}
