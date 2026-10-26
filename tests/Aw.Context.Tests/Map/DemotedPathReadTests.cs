using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

/// <summary>A file the repo demotes keeps its node and a direct read, but ranks after the product files in answers and lenses and is never an anchor or a suggestion (issue 1033).</summary>
public sealed class DemotedPathReadTests : IDisposable
{
    private const string Demoted = "src/a.ts";
    private const string Product = "src/b.ts";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Demoting(params string[] globs) =>
        new(Settings: null, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DemotePaths = [.. globs] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    private async Task Import() =>
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"code:src/a.ts\", label: \"Zeta handler\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "edges:",
            "  - {from: \"code:src/a.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}"));

    [Fact, Trait("Lane", "unit")]
    public async Task A_demoted_file_stays_in_the_lenses_after_the_product_files()
    {
        await Import();

        var plain = (await _world.Engine.Lenses("feature:kept", read: MapRead.Current, depth: null, MapWorld.Human, MapWorld.None)).Value;
        var demoted = (await _world.Engine.Lenses("feature:kept", read: MapRead.Current, depth: null, Demoting(Demoted), MapWorld.None)).Value;

        Assert.Equal([Demoted, Product], plain.Bands.Code.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal([Product, Demoted], demoted.Bands.Code.Files.Select(f => f.Path), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_query_puts_a_demoted_file_s_code_item_last()
    {
        await Import();

        var plain = (await _world.Engine.Query(new ContextQuery(Text: "Kept"), MapWorld.Human, MapWorld.None)).Value;
        var demoted = (await _world.Engine.Query(new ContextQuery(Text: "Kept"), Demoting(Demoted), MapWorld.None)).Value;

        Assert.Equal(["code:" + Demoted, "code:" + Product], CodeNodes(plain), StringComparer.Ordinal);
        Assert.Equal(["code:" + Product, "code:" + Demoted], CodeNodes(demoted), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_demoted_file_is_not_offered_as_an_anchor()
    {
        await Import();

        var plain = (await _world.Engine.Query(new ContextQuery(Text: "Kept Zeta handler"), MapWorld.Human, MapWorld.None)).Value;
        var demoted = (await _world.Engine.Query(new ContextQuery(Text: "Kept Zeta handler"), Demoting(Demoted), MapWorld.None)).Value;

        Assert.Contains("code:" + Demoted, plain.Anchors, StringComparer.Ordinal);
        Assert.DoesNotContain("code:" + Demoted, demoted.Anchors, StringComparer.Ordinal);
        Assert.DoesNotContain("code:" + Demoted, demoted.SuggestedAnchors, StringComparer.Ordinal);
        Assert.Contains("feature:kept", demoted.Anchors, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_direct_read_of_a_demoted_file_s_node_still_finds_it()
    {
        await Import();

        var shown = await _world.Engine.ShowNode("code:" + Demoted, Demoting(Demoted), MapWorld.None);
        var queried = await _world.Engine.Query(new ContextQuery(Text: null, Node: "code:" + Demoted), Demoting(Demoted), MapWorld.None);

        Assert.False(shown.IsError);
        Assert.False(queried.IsError);
        Assert.Contains("code:" + Demoted, queried.Value.Anchors, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_anchor_error_never_suggests_a_demoted_file()
    {
        await Import();

        var plain = await _world.Engine.Lenses("Zeta thing", read: MapRead.Current, depth: null, MapWorld.Human, MapWorld.None);
        var demoted = await _world.Engine.Lenses("Zeta thing", read: MapRead.Current, depth: null, Demoting(Demoted), MapWorld.None);

        Assert.Contains(plain.Error.Extensions ?? Kernel.ErrorExtensions.Empty, m => m.Key == "suggestedAnchors" && m.Value.ToString().Contains("code:" + Demoted, StringComparison.Ordinal));
        Assert.DoesNotContain(demoted.Error.Extensions ?? Kernel.ErrorExtensions.Empty, m => m.Key == "suggestedAnchors");
    }

    private static List<string?> CodeNodes(ContextResult result) =>
        [.. result.Sections.Where(s => string.Equals(s.Name, "code", StringComparison.Ordinal)).SelectMany(s => s.Items).Select(i => i.Node)];
}
