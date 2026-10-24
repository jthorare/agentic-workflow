using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Symbols;
using Aw.Context.Tests.Contract;
using Aw.Context.Tests.GetCode;
using Aw.Context.Tests.Map;
using Aw.Kernel.Testing;

namespace Aw.Context.Tests.GetFileMap;

/// <summary>The file map's 'symbols' column from the code index (S-9a, on a fake): the files listed, and the empty states with their words.</summary>
public sealed class GetFileMapSymbolsTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From(FixtureMap.Repo);
    private const string MapCommit = MapWorld.Head;
    private const string AheadCommit = "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718293";

    private static readonly Dictionary<string, string> _tree = new(StringComparer.Ordinal)
    {
        ["src/tabs/TabManager.ts"] = "export const a = 1;",
        ["src/tabs/TabGroups.ts"] = "export const b = 1;",
        ["src/layout/Grid.ts"] = "export const d = 1;",
        ["src/layout/inner/Deep.ts"] = "export const e = 1;",
        ["README.md"] = "# app",
    };

    private static readonly SymbolSite _manager = new("src/tabs/TabManager.ts#restoreLayout", "restoreLayout", "method", "src/tabs/TabManager.ts", 4, 20, "restoreLayout(): void", Exported: true);
    private static readonly SymbolSite _groups = new("src/tabs/TabGroups.ts#group", "group", "function", "src/tabs/TabGroups.ts", StartLine: 1, EndLine: 3, Signature: null, Exported: false);
    private static readonly SymbolSite _deep = new("src/layout/inner/Deep.ts#deep", "deep", "function", "src/layout/inner/Deep.ts", StartLine: 1, EndLine: 3, Signature: null, Exported: false);

    private static FakeIndexedRepo Index(string commit, CodeIndexState state = CodeIndexState.Ok) => new(_repo, state, commit, [_manager, _groups, _deep], []);

    private static async Task<(MapWorld World, FileMapView View)> Ask(ICodeSymbolSource source, string path = "src/tabs/", string? mapCommit = MapCommit)
    {
        var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, _tree), symbols: source);
        _ = await world.ImportYaml((mapCommit is null ? string.Empty : $"commit: {mapCommit}\n") + "schemaVersion: 1\nkind: authored\nrepo: aw-split-panes\nnodes:\n  - {key: \"feature:tabs\", label: \"Tabs\"}\n");
        return (world, (await world.Engine.FileMap(new FileMapQuery(_repo, path, UnmappedOnly: false, Draft: false), MapWorld.Human, MapWorld.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_at_the_maps_commit_gives_each_listed_file_its_symbols()
    {
        var (world, view) = await Ask(new FakeCodeSymbolSource([Index(MapCommit)]));
        using var _ = world;

        Assert.Equal((SymbolsState.Ok, null, MapCommit, null, false), (view.Symbols.State, view.Symbols.Reason, view.Symbols.Commit, view.Symbols.AsOfCommit, view.Symbols.AsOfMismatch));
        Assert.Equal(["src/tabs/TabGroups.ts", "src/tabs/TabManager.ts"], view.Symbols.Files!.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(["restoreLayout"], view.Symbols.Files!.Single(f => f.Path.EndsWith("TabManager.ts", StringComparison.Ordinal)).Symbols.Select(s => s.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_the_files_listed_are_asked_for_not_the_files_under_a_directory()
    {
        var recording = new RecordingSymbolSource(new FakeCodeSymbolSource([Index(MapCommit)]));
        var (world, _) = await Ask(recording, path: "src/");
        using var __ = world;

        // src/ lists two directories and no file: the column asks for nothing, and still reads the head for its words
        Assert.DoesNotContain(recording.Calls, c => c.Call == "files");
        Assert.Contains(recording.Calls, c => c.Call == "heads");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_index_is_the_unavailable_the_map_always_had()
    {
        var (world, view) = await Ask(new FakeCodeSymbolSource([]));
        using var _ = world;

        Assert.Equal(LensSymbols.Unavailable with { Code = "SND-CODEIDX-013" }, view.Symbols);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_build_in_progress_says_index_building()
    {
        var (world, view) = await Ask(new FakeCodeSymbolSource([new FakeIndexedRepo(_repo, CodeIndexState.Building, Commit: null, [], [])]));
        using var _ = world;

        Assert.Equal((SymbolsState.Unavailable, "index building"), (view.Symbols.State, view.Symbols.Reason));
        Assert.Null(view.Symbols.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_index_still_gives_its_symbols_and_says_stale()
    {
        var (world, view) = await Ask(new FakeCodeSymbolSource([Index(MapCommit, CodeIndexState.Stale)]));
        using var _ = world;

        Assert.Equal((SymbolsState.Stale, "stale", MapCommit, null, false), (view.Symbols.State, view.Symbols.Reason, view.Symbols.Commit, view.Symbols.AsOfCommit, view.Symbols.AsOfMismatch));
        Assert.Equal(2, view.Symbols.Files!.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_published_ahead_of_its_index_names_both_commits()
    {
        var (world, view) = await Ask(new FakeCodeSymbolSource([Index(AheadCommit)]));
        using var _ = world;

        Assert.Equal((SymbolsState.Stale, "stale", AheadCommit, MapCommit, true), (view.Symbols.State, view.Symbols.Reason, view.Symbols.Commit, view.Symbols.AsOfCommit, view.Symbols.AsOfMismatch));
        Assert.All(view.Symbols.Files!, f => Assert.Equal((AheadCommit, MapCommit, SymbolsState.Stale), (f.Commit, f.AsOfCommit, f.State)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_read_that_fails_is_unavailable_with_the_errors_code_and_the_tree_still_lists()
    {
        var (world, view) = await Ask(new FailingSymbolSource());
        using var _ = world;

        Assert.Equal((SymbolsState.Unavailable, "index unavailable", ContextErrors.ExtractionUnreadable.Code), (view.Symbols.State, view.Symbols.Reason, view.Symbols.Code));
        Assert.Equal(2, view.Entries.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_listing_of_many_files_asks_for_at_most_1000()
    {
        var wide = Enumerable.Range(0, 1_100).ToDictionary(i => $"src/wide/f{i.ToString("D3", CultureInfo.InvariantCulture)}.ts", _ => "export {};", StringComparer.Ordinal);
        var recording = new RecordingSymbolSource(new FakeCodeSymbolSource([Index(MapCommit)]));
        using var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, wide), symbols: recording);
        _ = await world.ImportYaml($"commit: {MapCommit}\nschemaVersion: 1\nkind: authored\nrepo: aw-split-panes\nnodes:\n  - {{key: \"feature:tabs\", label: \"Tabs\"}}\n");

        _ = await world.Engine.FileMap(new FileMapQuery(_repo, "src/wide/", UnmappedOnly: false, Draft: false), MapWorld.Human, MapWorld.None);

        Assert.Equal(1_000, recording.Calls.Single(c => c.Call == "files").Subject!.Split(',').Length);
    }

    // Golden files: the file map as the wire carries it, with symbols from a fresh index and from one the map is ahead of.
    [Fact, Trait("Lane", "unit")]
    public async Task The_file_map_over_a_fresh_index_matches_its_golden_file()
    {
        var (world, view) = await Ask(new FakeCodeSymbolSource([Index(MapCommit)]));
        using var _ = world;

        Golden.Match(GoldenJson.Of(view), typeof(GetFileMapSymbolsTests).Assembly, "GetFileMap/Golden/filemap-symbols-fresh.json", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_file_map_over_an_index_the_map_is_ahead_of_matches_its_golden_file()
    {
        var (world, view) = await Ask(new FakeCodeSymbolSource([Index(AheadCommit)]));
        using var _ = world;

        Golden.Match(GoldenJson.Of(view), typeof(GetFileMapSymbolsTests).Assembly, "GetFileMap/Golden/filemap-symbols-as-of-mismatch.json", Normalization.None);
    }
}
