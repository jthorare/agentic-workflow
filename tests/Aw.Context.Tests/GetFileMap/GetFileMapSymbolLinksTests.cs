using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Symbols;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetFileMap;

/// <summary>Phase 9: the file map's file-level 'implements' refined to symbols from the code index as of the map's commit, falling back to file level when the index cannot answer.</summary>
public sealed class GetFileMapSymbolLinksTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From(FixtureMap.Repo);

    private static readonly Dictionary<string, string> _tree = new(StringComparer.Ordinal)
    {
        ["src/tabs/TabManager.ts"] = "export class TabManager {}",
        ["src/tabs/TabGroups.ts"] = "export const b = 1;",
    };

    private static readonly SymbolSite _manager = new("src/tabs/TabManager.ts#TabManager", "TabManager", "class", "src/tabs/TabManager.ts", 1, 9, Signature: null, Exported: true);
    private static readonly SymbolSite _other = new("src/tabs/TabManager.ts#helper", "helper", "function", "src/tabs/TabManager.ts", 11, 12, Signature: null, Exported: false);

    private static async Task<FileMapView> Ask(FakeIndexedRepo? index)
    {
        var source = new FakeCodeSymbolSource(index is null ? [] : [index]);
        using var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, _tree), symbols: source);
        _ = await world.ImportYaml(string.Concat("{\"commit\": \"", MapWorld.Head, "\", ", MapWorld.TabsMap.TrimStart().AsSpan(1)));
        return (await world.Engine.FileMap(new FileMapQuery(_repo, "src/tabs/", UnmappedOnly: false, Draft: false), MapWorld.Human, MapWorld.None)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_level_link_is_refined_to_the_symbol_that_names_its_node()
    {
        var view = await Ask(new FakeIndexedRepo(_repo, CodeIndexState.Ok, MapWorld.Head, [_manager, _other], []));

        var link = Assert.Single(view.Symbols.Implements!);
        Assert.Equal(("feature:tabs.tab-manager", "src/tabs/TabManager.ts#TabManager", 0.9), (link.Node.Value, link.Ref, link.Confidence));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_index_still_refines_and_says_stale_with_both_commits()
    {
        var view = await Ask(new FakeIndexedRepo(_repo, CodeIndexState.Ok, "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718293", [_manager], []));

        Assert.Equal((SymbolsState.Stale, true), (view.Symbols.State, view.Symbols.AsOfMismatch));
        _ = Assert.Single(view.Symbols.Implements!);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_index_leaves_the_links_at_file_level_and_the_map_unharmed()
    {
        var view = await Ask(index: null);

        Assert.Null(view.Symbols.Implements);
        Assert.Equal(SymbolsState.Unavailable, view.Symbols.State);
        Assert.Contains(view.Entries, e => e.Name == "TabManager.ts" && e.Features.Count > 0);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_with_no_symbol_named_like_the_node_gives_an_empty_refinement_not_an_error()
    {
        var view = await Ask(new FakeIndexedRepo(_repo, CodeIndexState.Ok, MapWorld.Head, [_other], []));

        Assert.Empty(view.Symbols.Implements!);
    }
}
